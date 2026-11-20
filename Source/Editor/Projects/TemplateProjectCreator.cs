using System.Diagnostics;
using System.Text;

namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>
/// Creates projects from the engine's <c>dotnet new flickervoid</c> template, installing it from the engine folder that
/// <c>FLICKERVOID_ENGINE</c> names, where builds find the engine too.
/// </summary>
public sealed class TemplateProjectCreator : IProjectCreator
{
    public const string EngineVariable = "FLICKERVOID_ENGINE";

    public string Create(string name, string folder)
    {
        string? engine = Environment.GetEnvironmentVariable(EngineVariable);
        if (string.IsNullOrEmpty(engine))
        {
            throw new ProjectBuildException($"Set {EngineVariable} to the engine's folder: new projects build against it.");
        }

        string template = Path.Combine(engine, "Templates", "Project");
        Run("new", "install", template, "--force");
        string output = Path.Combine(folder, name);
        Run("new", "flickervoid", "-n", name, "--engine", engine, "-o", output);
        return Path.Combine(output, name + EditorProject.Extension);
    }

    private static void Run(params string[] arguments)
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

        using Process process = Process.Start(start) ?? throw new ProjectBuildException("dotnet could not be started.");
        StringBuilder output = new();
        process.OutputDataReceived += (_, e) => output.AppendLine(e.Data);
        process.ErrorDataReceived += (_, e) => output.AppendLine(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            string command = string.Join(' ', arguments);
            throw new ProjectBuildException($"dotnet {command} failed:\n{output}");
        }
    }
}
