using System.Text;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>Finds and creates material files (<c>Name.fvmat</c>) in a content folder, named by their path in it.</summary>
public sealed class MaterialBrowser(IContentManager content)
{
    private readonly string _folder = content.RootDirectory;

    public const string Extension = ".fvmat";

    /// <summary>The materials whose path holds the text, ignoring case, sorted, with forward slashes.</summary>
    public IReadOnlyList<string> Find(string text)
    {
        if (!Directory.Exists(_folder))
        {
            return [];
        }

        List<string> found = [];
        foreach (string fullPath in Directory.EnumerateFiles(_folder, "*" + Extension, SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(_folder, fullPath);
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
        while (File.Exists(Path.Combine(_folder, path)))
        {
            path = $"{name} {number}{Extension}";
            number++;
        }

        byte[] contents = Encoding.UTF8.GetBytes("""{ "parameters": {} }""");
        content.Write(path, contents);
        return path;
    }
}
