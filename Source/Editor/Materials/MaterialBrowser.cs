namespace FlickerVoid.Engine.Editor.Materials;

/// <summary>Finds and creates material files (<c>Name.material.json</c>) in a content folder, named by their path in it.</summary>
public sealed class MaterialBrowser(string folder)
{
    public const string Extension = ".material.json";

    /// <summary>The materials whose path holds the text, ignoring case, sorted, with forward slashes.</summary>
    public IReadOnlyList<string> Find(string text)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        List<string> found = [];
        foreach (string fullPath in Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(folder, fullPath);
            string path = relative.Replace(Path.DirectorySeparatorChar, '/');
            if (path.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(path);
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>Writes a material that keeps every default, numbering the name when it is taken, and returns its path.</summary>
    public string Create(string name)
    {
        string path = name + Extension;
        int number = 2;
        while (File.Exists(Path.Combine(folder, path)))
        {
            path = $"{name} {number}{Extension}";
            number++;
        }

        string fullPath = Path.Combine(folder, path);
        File.WriteAllText(fullPath, """{ "parameters": {} }""");
        return path;
    }
}
