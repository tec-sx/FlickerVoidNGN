using System.Diagnostics;
using System.Text;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>Runs <c>dotnet</c> commands with <c>FLICKERVOID_ENGINE</c> set to the engine folder, so builds find the engine.</summary>
internal static class DotnetCli
{
    public const string EngineVariable = "FLICKERVOID_ENGINE";

    public static readonly string TemplateFolder = Path.Combine("Templates", "Project");

    private static readonly string[] MSBuildVariables = ["MSBuildSDKsPath", "MSBuildExtensionsPath", "MSBUILD_EXE_PATH"];

    private static readonly Lazy<string> _engine = new(ResolveEngine);

    public static string Engine => _engine.Value;

    public static string? FindEngine(string? variable, string start)
    {
        if (!string.IsNullOrEmpty(variable))
        {
            return variable;
        }

        for (DirectoryInfo? dir = new(start); dir != null; dir = dir.Parent)
        {
            string template = Path.Combine(dir.FullName, TemplateFolder);
            if (Directory.Exists(template))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    public static string Run(params string[] arguments) => Run(null, arguments);

    /// <summary>
    /// Runs <c>dotnet</c> and returns its output, passing each line to <paramref name="onLine"/> as it comes; throws
    /// <see cref="ProjectBuildException"/> with the output when dotnet fails.
    /// </summary>
    public static string Run(Action<string>? onLine, params string[] arguments)
    {
        ProcessStartInfo start = CreateStartInfo(arguments);

        using Process process = Process.Start(start) ?? throw new ProjectBuildException("dotnet could not be started.");
        StringBuilder output = new();
        Lock outputLock = new();
        void Add(string? line)
        {
            // Standard output and error arrive on two threads; the end of each stream comes as null.
            if (line is null)
            {
                return;
            }

            lock (outputLock)
            {
                output.AppendLine(line);
                onLine?.Invoke(line);
            }
        }

        process.OutputDataReceived += (_, e) => Add(e.Data);
        process.ErrorDataReceived += (_, e) => Add(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        string text = output.ToString();
        if (process.ExitCode != 0)
        {
            string command = string.Join(' ', arguments);
            throw new ProjectBuildException($"dotnet {command} failed:\n{text}");
        }

        return text;
    }

    /// <summary>
    /// A <c>dotnet</c> process with the engine variable set and without the MSBuild paths an IDE (or a stale system setting)
    /// may have put in the editor's environment: they point MSBuild at one SDK's files while <c>dotnet</c> runs another.
    /// </summary>
    public static ProcessStartInfo CreateStartInfo(IEnumerable<string> arguments)
    {
        ProcessStartInfo start = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (string variable in MSBuildVariables)
        {
            start.Environment.Remove(variable);
        }

        start.Environment[EngineVariable] = Engine;
        return start;
    }

    private static string ResolveEngine()
    {
        string? variable = Environment.GetEnvironmentVariable(EngineVariable);
        string? engine = FindEngine(variable, AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(engine))
        {
            throw new ProjectBuildException($"Set {EngineVariable} to the engine's folder: new projects build against it.");
        }

        return engine;
    }
}
