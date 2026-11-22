using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// Lists the files and folders of a content folder for the asset browser, skipping hidden ones (a leading dot, such as the
/// schemas and build caches), and tells each file's kind by its extension. Paths are content paths: a source folder built into
/// a folder of the content folder lists its files under that folder's path, the <paramref name="prefix"/>. A code catalog
/// (<see cref="ForCode"/>) lists a game project's source folder instead, without its build output.
/// </summary>
public sealed class AssetCatalog(string root, string prefix = "", string? name = null)
{
    /// <summary>The path a code catalog lists its files under, which no content path starts with.</summary>
    public const string CodePrefix = "/Code";

    private static readonly (string Suffix, AssetKind Kind)[] Suffixes =
    [
        (".fvscene", AssetKind.Scene),
        (".fvmat", AssetKind.Material),
        (".fvshader", AssetKind.Shader),
        (".fvmood", AssetKind.Mood),
        (".fvcanvas", AssetKind.Canvas),
        (".fvdata", AssetKind.Data),
        (".fvtable", AssetKind.Table),
        (".fvflow", AssetKind.Flow),
        (".glb", AssetKind.Model),
        (".gltf", AssetKind.Model),
        (".fvmodel", AssetKind.Model),
        (".png", AssetKind.Texture),
        (".jpg", AssetKind.Texture),
        (".jpeg", AssetKind.Texture),
        (".tga", AssetKind.Texture),
        (".bmp", AssetKind.Texture),
        (".reb", AssetKind.Texture),
        (".dds", AssetKind.Texture),
        (".hlsl", AssetKind.Shader),
        (".hlsli", AssetKind.Shader),
        (".spv", AssetKind.Shader),
        (".cs", AssetKind.Code),
    ];

    public string Root { get; } = root;

    /// <summary>Whether this catalog lists code, which the content manager doesn't hold.</summary>
    public bool IsCode => Prefix == CodePrefix;

    /// <summary>The content path of <see cref="Root"/>, with forward slashes; empty for the content folder itself.</summary>
    public string Prefix { get; } = prefix;

    /// <summary>The visible folders as a tree, sorted by name, under a root named after the folder.</summary>
    public AssetFolder GetFolders()
    {
        string rootName = name ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(Root));
        return ReadFolder(Root, Prefix, rootName);
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
        string fullFolder = GetFullPath(folder);
        List<AssetEntry> found = [];
        if (!Directory.Exists(fullFolder))
        {
            return found;
        }

        Collect(fullFolder, recursive, kind, text, found);
        found.Sort(static (first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Path, second.Path));
        return found;
    }

    /// <summary>The full path of a path this catalog holds.</summary>
    public string GetFullPath(string path)
    {
        string local = GetLocalPath(path);
        string relative = local.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Root, relative);
    }

    /// <summary>A path this catalog holds, below its root: "World/Doors" for "/Code/World/Doors".</summary>
    public string GetLocalPath(string path)
    {
        if (Prefix.Length == 0)
        {
            return path;
        }

        if (path.Length > Prefix.Length)
        {
            return path[(Prefix.Length + 1)..];
        }

        return "";
    }

    /// <summary>A catalog over a game project's source folder, named Code, under <see cref="CodePrefix"/>.</summary>
    public static AssetCatalog ForCode(string folder) => new(folder, CodePrefix, "Code");

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
            if (IsHidden(fullPath) || fullPath.EndsWith(TextureSettings.Extension, StringComparison.OrdinalIgnoreCase))
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
            if (!IsSkipped(subfolder))
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
                if (IsSkipped(subfolder))
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

    /// <summary>Hidden folders, and in code the build output.</summary>
    private bool IsSkipped(string fullFolder)
    {
        if (IsHidden(fullFolder))
        {
            return true;
        }

        string folderName = Path.GetFileName(fullFolder);
        return IsCode && (folderName is "bin" or "obj");
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
