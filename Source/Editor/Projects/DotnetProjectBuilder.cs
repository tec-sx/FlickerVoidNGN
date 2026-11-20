using System.Diagnostics;
using System.Text;

namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Builds a game with <c>dotnet build</c>, then asks MSBuild for its output folder.</summary>
public sealed class DotnetProjectBuilder : IProjectBuilder
{
    public string Build(string gameProject)
    {
        string output = Run("build", gameProject, "-nologo", "-v:minimal");
        string targetDir = Run("msbuild", gameProject, "-nologo", "-getProperty:TargetDir");
        string folder = targetDir.Trim();
        if (folder.Length == 0)
        {
            throw new ProjectBuildException($"{gameProject} built, but MSBuild named no output folder.\n{output}");
        }

        return folder;
    }

    private static string Run(params string[] arguments)
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
        string text = output.ToString();
        if (process.ExitCode != 0)
        {
            string command = string.Join(' ', arguments);
            throw new ProjectBuildException($"dotnet {command} failed:\n{text}");
        }

        return text;
    }
}
