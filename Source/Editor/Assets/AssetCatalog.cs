using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// Lists the files and folders of a content folder for the asset browser, skipping hidden ones (a leading dot, such as the
/// schemas and build caches), and tells each file's type by its extension (<see cref="AssetTypes"/>). A file owns the files in
/// its folder named after it (<see cref="AssetType.IsCreation"/>), which are listed with it rather than on their own. Paths are
/// content paths: a source folder built into a folder of the content folder lists its files under that folder's path, the
/// <paramref name="prefix"/>. A code catalog (<see cref="ForCode"/>) lists a game project's source folder instead, without its
/// build output.
/// </summary>
public sealed class AssetCatalog(string root, string prefix = "", string? name = null)
{
    /// <summary>The path a code catalog lists its files under, which no content path starts with.</summary>
    public const string CodePrefix = "/Code";

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
    /// The visible files in the folder (or below it too when <paramref name="recursive"/>) of the type, or of any type when
    /// null, whose name holds the text, ignoring case; sorted by path. A file owned by one that doesn't match is listed on its own
    /// when it matches.
    /// </summary>
    public IReadOnlyList<AssetEntry> Find(string folder, bool recursive, AssetType? type, string text)
    {
        IReadOnlyList<AssetType> types = [];
        if (type is not null)
        {
            types = [type];
        }

        return Find(folder, recursive, types, text);
    }

    /// <summary>As the search by one type, for a filter's types; none means any type.</summary>
    public IReadOnlyList<AssetEntry> Find(string folder, bool recursive, IReadOnlyList<AssetType> types, string text)
    {
        string fullFolder = GetFullPath(folder);
        List<AssetEntry> found = [];
        if (!Directory.Exists(fullFolder))
        {
            return found;
        }

        Collect(fullFolder, recursive, types, text, found);
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

    private void Collect(string fullFolder, bool recursive, IReadOnlyList<AssetType> types, string text, List<AssetEntry> found)
    {
        List<AssetEntry> groups = ReadGroups(fullFolder);
        foreach (AssetEntry entry in groups)
        {
            if (Matches(entry, types, text))
            {
                found.Add(entry);
                continue;
            }

            foreach (AssetEntry owned in entry.Owned)
            {
                if (Matches(owned, types, text))
                {
                    found.Add(owned);
                }
            }
        }

        if (!recursive)
        {
            return;
        }

        foreach (string subfolder in Directory.EnumerateDirectories(fullFolder))
        {
            if (!IsSkipped(subfolder))
            {
                Collect(subfolder, recursive, types, text, found);
            }
        }
    }

    private static bool Matches(AssetEntry entry, IReadOnlyList<AssetType> types, string text)
    {
        return entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) && (types.Count == 0 || types.Contains(entry.Type));
    }

    /// <summary>The folder's visible files that no other file owns, each with the files it owns, sorted by name.</summary>
    private List<AssetEntry> ReadGroups(string fullFolder)
    {
        List<string> fileNames = [];
        foreach (string fullPath in Directory.EnumerateFiles(fullFolder))
        {
            if (!IsHidden(fullPath))
            {
                string fileName = Path.GetFileName(fullPath);
                fileNames.Add(fileName);
            }
        }

        fileNames.Sort(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> owners = [];
        foreach (string fileName in fileNames)
        {
            if (FindOwner(fileName, fileNames) is { } owner)
            {
                owners.Add(fileName, owner);
            }
        }

        List<AssetEntry> groups = [];
        foreach (string fileName in fileNames)
        {
            if (owners.ContainsKey(fileName))
            {
                continue;
            }

            AssetEntry entry = CreateEntry(fullFolder, fileName);
            List<AssetEntry> owned = [];
            foreach (string ownedName in fileNames)
            {
                if (owners.GetValueOrDefault(ownedName) == fileName)
                {
                    AssetEntry file = CreateEntry(fullFolder, ownedName);
                    owned.Add(file with { Owner = entry.Path });
                }
            }

            groups.Add(entry with { Owned = owned });
        }

        return groups;
    }

    /// <summary>
    /// The file that owns the named one: of the files whose key (<see cref="GetKey"/>) and a dot start its name, the one with the
    /// shortest key, shorter than its own, so the files of a group all start with the group's name.
    /// </summary>
    private static string? FindOwner(string fileName, List<string> fileNames)
    {
        string? owner = null;
        int ownerKeyLength = GetKey(fileName).Length;
        foreach (string other in fileNames)
        {
            string key = GetKey(other);
            if (key.Length < ownerKeyLength && fileName.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase))
            {
                owner = other;
                ownerKeyLength = key.Length;
            }
        }

        return owner;
    }

    /// <summary>What a file's owned files are named after: a creation's name without extensions, else the whole file name.</summary>
    private static string GetKey(string fileName)
    {
        AssetType type = AssetTypes.Find(fileName);
        if (type.IsCreation)
        {
            return GetName(fileName);
        }

        return fileName;
    }

    private AssetEntry CreateEntry(string fullFolder, string fileName)
    {
        string fullPath = Path.Combine(fullFolder, fileName);
        string path = ToContentPath(fullPath);
        string name = GetName(fileName);
        AssetType type = AssetTypes.Find(fileName);
        string? thumbnail = null;
        if (IsDrawnImage(fileName))
        {
            thumbnail = fullPath;
        }

        return new AssetEntry(path, name, type) { Thumbnail = thumbnail };
    }

    /// <summary>Whether the browser can decode the image for a thumbnail (the formats Avalonia reads).</summary>
    private static bool IsDrawnImage(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
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
    private static string GetName(string fileName)
    {
        string name = Path.GetFileName(fileName);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0)
        {
            return name[..dot];
        }

        return name;
    }
}
