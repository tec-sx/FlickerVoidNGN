using System.Diagnostics;

namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Starts a built game with <c>dotnet &lt;program&gt;.dll</c>, or an executable directly, in its own folder.</summary>
public sealed class ProcessGameLauncher : IGameLauncher
{
    public void Launch(string program, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = new(program) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(program)! };
        if (program.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            start.FileName = "dotnet";
            start.ArgumentList.Add(program);
        }

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{program} could not be started.");
    }
}
