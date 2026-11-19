using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>
/// Lists the files and folders of a content folder for the asset browser, skipping hidden ones (a leading dot, such as the
/// schemas and build caches), and tells each file's kind by its extension. Paths are content paths: a source folder built into
/// a folder of the content folder lists its files under that folder's path, the <paramref name="prefix"/>.
/// </summary>
public sealed class AssetCatalog(string root, string prefix = "")
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

    /// <summary>The content path of <see cref="Root"/>, with forward slashes; empty for the content folder itself.</summary>
    public string Prefix { get; } = prefix;

    /// <summary>The visible folders as a tree, sorted by name, under a root named after the folder.</summary>
    public AssetFolder GetFolders()
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Root));
        return ReadFolder(Root, Prefix, name);
    }

    /// <summary>Whether the content path is this catalog's root or below it.</summary>
    public bool Holds(string path)
    {
        if (Prefix.Length == 0)
        {
            return true;
        }

        return path == Prefix || path.StartsWith(Prefix + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// The visible files in the folder (or below it too when <paramref name="recursive"/>) of the kind, or of any kind when
    /// null, whose name holds the text, ignoring case; sorted by path.
    /// </summary>
    public IReadOnlyList<AssetEntry> Find(string folder, bool recursive, AssetKind? kind, string text)
    {
        string local = folder;
        if (Prefix.Length > 0)
        {
            local = folder.Length > Prefix.Length ? folder[(Prefix.Length + 1)..] : "";
        }

        string fullFolder = Path.Combine(Root, local);
        List<AssetEntry> found = [];
        if (!Directory.Exists(fullFolder))
        {
            return found;
        }

        Collect(fullFolder, recursive, kind, text, found);
        found.Sort(static (first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Path, second.Path));
        return found;
    }

    /// <summary>One catalog per game source folder the build manifest names, else one over the content folder.</summary>
    public static List<AssetCatalog> ForContent(IContentManager content)
    {
        List<AssetCatalog> catalogs = [];
        ContentBuildManifest? manifest = ContentBuildManifest.Find(content.RootDirectory);
        if (manifest is not null)
        {
            foreach (ContentBuildFolder folder in manifest.Folders)
            {
                if (folder.Engine)
                {
                    continue;
                }

                string relative = Path.GetRelativePath(content.RootDirectory, folder.Output);
                string prefix = relative.Replace(Path.DirectorySeparatorChar, '/');
                if (prefix == ".")
                {
                    prefix = "";
                }

                catalogs.Add(new AssetCatalog(folder.Source, prefix));
            }
        }

        if (catalogs.Count == 0)
        {
            catalogs.Add(new AssetCatalog(content.RootDirectory));
        }

        return catalogs;
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
        string path = relative.Replace(Path.DirectorySeparatorChar, '/');
        if (Prefix.Length == 0)
        {
            return path;
        }

        return $"{Prefix}/{path}";
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
