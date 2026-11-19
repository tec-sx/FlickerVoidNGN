namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>
/// Lists the files and folders of a content folder for the asset browser, skipping hidden ones (a leading dot, such as the
/// schemas and build caches), and tells each file's kind by its extension.
/// </summary>
public sealed class AssetCatalog(string root)
{
    private static readonly (string Suffix, AssetKind Kind)[] Suffixes =
    [
        (".fvscene", AssetKind.Scene),
        (".fvmat", AssetKind.Material),
        (".fvshader", AssetKind.Shader),
        (".fvmood", AssetKind.Mood),
        (".glb", AssetKind.Model),
        (".gltf", AssetKind.Model),
        (".fvmodel", AssetKind.Model),
        (".png", AssetKind.Texture),
        (".jpg", AssetKind.Texture),
        (".jpeg", AssetKind.Texture),
        (".tga", AssetKind.Texture),
        (".bmp", AssetKind.Texture),
        (".dds", AssetKind.Texture),
        (".hlsl", AssetKind.Shader),
        (".hlsli", AssetKind.Shader),
        (".spv", AssetKind.Shader),
    ];

    public string Root { get; } = root;

    /// <summary>The content folder's visible folders as a tree, sorted by name.</summary>
    public AssetFolder GetFolders()
    {
        return ReadFolder(Root, "", "");
    }

    /// <summary>
    /// The visible files in the folder (or below it too when <paramref name="recursive"/>) of the kind, or of any kind when
    /// null, whose name holds the text, ignoring case; sorted by path.
    /// </summary>
    public IReadOnlyList<AssetEntry> Find(string folder, bool recursive, AssetKind? kind, string text)
    {
        string fullFolder = Path.Combine(Root, folder);
        List<AssetEntry> found = [];
        if (!Directory.Exists(fullFolder))
        {
            return found;
        }

        Collect(fullFolder, recursive, kind, text, found);
        found.Sort(static (first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Path, second.Path));
        return found;
    }

    /// <summary>The kind of the file at the full path.</summary>
    public static AssetKind GetKind(string fullPath)
    {
        foreach ((string suffix, AssetKind kind) in Suffixes)
        {
            if (fullPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return AssetKind.Other;
    }

    private void Collect(string fullFolder, bool recursive, AssetKind? kind, string text, List<AssetEntry> found)
    {
        foreach (string fullPath in Directory.EnumerateFiles(fullFolder))
        {
            if (IsHidden(fullPath))
            {
                continue;
            }

            string name = GetName(fullPath);
            if (!name.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AssetKind fileKind = GetKind(fullPath);
            if (kind is not null && fileKind != kind)
            {
                continue;
            }

            string path = ToContentPath(fullPath);
            found.Add(new AssetEntry(path, name, fileKind));
        }

        if (!recursive)
        {
            return;
        }

        foreach (string subfolder in Directory.EnumerateDirectories(fullFolder))
        {
            if (!IsHidden(subfolder))
            {
                Collect(subfolder, recursive, kind, text, found);
            }
        }
    }

    private AssetFolder ReadFolder(string fullFolder, string path, string name)
    {
        List<AssetFolder> children = [];
        if (Directory.Exists(fullFolder))
        {
            foreach (string subfolder in Directory.EnumerateDirectories(fullFolder))
            {
                if (IsHidden(subfolder))
                {
                    continue;
                }

                string childPath = ToContentPath(subfolder);
                string childName = Path.GetFileName(subfolder);
                AssetFolder child = ReadFolder(subfolder, childPath, childName);
                children.Add(child);
            }
        }

        children.Sort(static (first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name));
        return new AssetFolder(path, name, children);
    }

    private string ToContentPath(string fullPath)
    {
        string relative = Path.GetRelativePath(Root, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsHidden(string fullPath)
    {
        string name = Path.GetFileName(fullPath);
        return name.StartsWith('.');
    }

    /// <summary>The file name without its extensions: Room for Room.fvscene.</summary>
    private static string GetName(string fullPath)
    {
        string name = Path.GetFileName(fullPath);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            return name[..dot];
        }

        return name;
    }
}
