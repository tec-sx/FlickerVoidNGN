using System.Text;
using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>
/// Finds and creates material files (<c>Name.fvmat</c>) in a content folder, named by their path in it, and arranges them in a
/// tree under the masters and materials they are instances of.
/// </summary>
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

    /// <summary>
    /// The materials whose path holds the text as a tree: each under its parent (a master is a root, listed when a material
    /// names it), with the parents of a match kept so it shows where it belongs, and a material whose parent can't be read a root.
    /// </summary>
    public IReadOnlyList<MaterialTreeNode> FindTree(string text)
    {
        IReadOnlyList<string> all = Find("");
        Dictionary<string, string?> parents = new(ContentManager.PathComparer);
        foreach (string path in all)
        {
            parents[path] = ReadParent(path);
        }

        HashSet<string> shown = new(ContentManager.PathComparer);
        foreach (string path in all)
        {
            if (path.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                Show(path, parents, shown);
            }
        }

        return BuildChildren(null, parents, shown);
    }

    /// <summary>
    /// Writes a material that keeps every default, or an instance of <paramref name="parent"/> that keeps all its values,
    /// numbering the name when it is taken, and returns its path.
    /// </summary>
    public string Create(string name, string? parent = null)
    {
        string path = name + Extension;
        int number = 2;
        while (File.Exists(Path.Combine(_folder, path)))
        {
            path = $"{name} {number}{Extension}";
            number++;
        }

        string json = """{ "parameters": {} }""";
        if (parent is not null)
        {
            JsonEncodedText encoded = JsonEncodedText.Encode(parent);
            json = $$"""{ "parent": "{{encoded}}", "parameters": {} }""";
        }

        byte[] contents = Encoding.UTF8.GetBytes(json);
        content.Write(path, contents);
        return path;
    }

    private static void Show(string path, Dictionary<string, string?> parents, HashSet<string> shown)
    {
        string? current = path;
        while (current is not null && shown.Add(current))
        {
            parents.TryGetValue(current, out current);
        }
    }

    /// <summary>The shown materials and masters whose parent is <paramref name="parent"/> (none for the roots), sorted, with theirs.</summary>
    private static List<MaterialTreeNode> BuildChildren(string? parent, Dictionary<string, string?> parents, HashSet<string> shown)
    {
        List<string> paths = [];
        foreach (string path in shown)
        {
            string? pathParent = null;
            if (parents.TryGetValue(path, out string? known) && known is not null && shown.Contains(known))
            {
                pathParent = known;
            }

            if (ContentManager.PathComparer.Equals(pathParent, parent))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        List<MaterialTreeNode> nodes = [];
        foreach (string path in paths)
        {
            List<MaterialTreeNode> children = BuildChildren(path, parents, shown);
            nodes.Add(new MaterialTreeNode(path, children));
        }

        return nodes;
    }

    private string? ReadParent(string path)
    {
        try
        {
            MaterialFile material = content.Load(path, RenderingJsonContext.Default.MaterialFile);
            return material.Parent;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }
}
