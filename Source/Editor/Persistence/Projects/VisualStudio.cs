using System.Diagnostics;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>Opens a project's solution in the newest Visual Studio installed (found by <c>vswhere</c>), else in whatever the system opens solutions with.</summary>
public static class VisualStudio
{
    /// <summary>Starts Visual Studio on <paramref name="solution"/> and returns at once.</summary>
    public static void Open(string solution)
    {
        string? devenv = FindDevenv();
        ProcessStartInfo start;
        if (devenv is null)
        {
            start = new ProcessStartInfo(solution) { UseShellExecute = true };
        }
        else
        {
            start = new ProcessStartInfo(devenv) { UseShellExecute = false };
            start.ArgumentList.Add(solution);
        }

        using Process? process = Process.Start(start);
    }

    private static string? FindDevenv()
    {
        if (!OperatingSystem.IsWindows())
        {
            // TODO(Linux): there is no Visual Studio; the solution opens in the IDE the desktop associates with it.
            return null;
        }

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string vswhere = Path.Combine(programs, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            return null;
        }

        ProcessStartInfo start = new(vswhere) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (string argument in (string[])["-latest", "-prerelease", "-property", "productPath"])
        {
            start.ArgumentList.Add(argument);
        }

        using Process? process = Process.Start(start);
        if (process is null)
        {
            return null;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        string devenv = output.Trim();
        if (devenv.Length == 0 || !File.Exists(devenv))
        {
            return null;
        }

        return devenv;
    }
}
