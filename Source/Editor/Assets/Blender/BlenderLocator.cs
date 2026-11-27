namespace FlickerVoid.Editor.Assets.Blender;

/// <summary>
/// Finds the Blender program: the one the <c>FLICKERVOID_BLENDER</c> environment variable names, else the one on the path, else the
/// newest under Blender's install folder (<c>Blender Foundation\Blender 5.3</c> on Windows).
/// </summary>
public static class BlenderLocator
{
    public const string Variable = "FLICKERVOID_BLENDER";

    public static string Program { get; } = OperatingSystem.IsWindows() ? "blender.exe" : "blender";

    public static string? Find()
    {
        string? named = Environment.GetEnvironmentVariable(Variable);
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] folders = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        List<string> installs = [];
        if (OperatingSystem.IsWindows())
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            installs.Add(Path.Combine(programs, "Blender Foundation"));
        }

        // TODO(Linux): look where Blender's archive is usually unpacked (/opt, ~/blender) once the owner's setup is known.
        return Find(named, folders, installs);
    }

    /// <summary>The program <paramref name="named"/> when it exists, else the first on the path, else the one in the last-named folder under an install root.</summary>
    public static string? Find(string? named, IReadOnlyList<string> pathFolders, IReadOnlyList<string> installRoots)
    {
        if (!string.IsNullOrEmpty(named) && File.Exists(named))
        {
            return named;
        }

        foreach (string folder in pathFolders)
        {
            string candidate = Path.Combine(folder, Program);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (string root in installRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> newestFirst = Directory.GetDirectories(root).OrderByDescending(folder => folder, StringComparer.OrdinalIgnoreCase);
            foreach (string folder in newestFirst)
            {
                string candidate = Path.Combine(folder, Program);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
