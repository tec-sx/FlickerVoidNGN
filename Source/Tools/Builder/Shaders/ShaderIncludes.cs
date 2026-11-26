using System.Text.RegularExpressions;

using FlickerVoid.Tools.Builder.Importing;

namespace FlickerVoid.Tools.Builder.Shaders;

/// <summary>Records an HLSL file and the files it includes as an import's dependencies, so it is built again when one changes.</summary>
public static partial class ShaderIncludes
{
    /// <summary>
    /// Records the file and, through its quoted includes, every file it reads, looked up in its own folder and then in
    /// <paramref name="includeDirectories"/>; a missing one is left to the compiler, and one outside the source folder (the
    /// engine's, for a game's master) is not recorded.
    /// </summary>
    public static void Record(AssetImportContext context, string fullPath, IReadOnlyList<string>? includeDirectories = null)
    {
        Record(context, fullPath, includeDirectories ?? [], []);
    }

    private static void Record(AssetImportContext context, string fullPath, IReadOnlyList<string> includeDirectories, HashSet<string> visited)
    {
        if (!File.Exists(fullPath) || !visited.Add(fullPath))
        {
            return;
        }

        if (context.IsSource(fullPath))
        {
            context.AddDependency(fullPath);
        }

        string folder = Path.GetDirectoryName(fullPath)!;
        string code = File.ReadAllText(fullPath);
        foreach (Match include in IncludePattern().Matches(code))
        {
            string name = include.Groups[1].Value;
            string includedPath = Find(name, folder, includeDirectories);
            Record(context, includedPath, includeDirectories, visited);
        }
    }

    private static string Find(string name, string folder, IReadOnlyList<string> includeDirectories)
    {
        string local = Path.GetFullPath(name, folder);
        if (File.Exists(local))
        {
            return local;
        }

        foreach (string directory in includeDirectories)
        {
            string candidate = Path.GetFullPath(name, directory);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return local;
    }

    [GeneratedRegex("""^\s*#\s*include\s*"([^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex IncludePattern();
}
