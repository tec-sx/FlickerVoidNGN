using System.Text.Json;

namespace FlickerVoid.Engine.Editor.Assets;

/// <summary>
/// Lists the files and folders of a content folder for the asset browser, skipping hidden ones (a leading dot, such as the
/// schemas and build caches), and tells each file's kind: by its extensions, or, for a plain JSON file, by the schema it names.
/// </summary>
public sealed class AssetCatalog(string root)
{
    private const string JsonExtension = ".json";
    private const string SchemaSuffix = ".schema.json";

    // Longest suffixes first, so Name.scene.json is a scene rather than plain JSON.
    private static readonly (string Suffix, AssetKind Kind)[] Suffixes =
    [
        (".scene.json", AssetKind.Scene),
        (".material.json", AssetKind.Material),
        (".shader.json", AssetKind.Shader),
        (".mood.json", AssetKind.Mood),
        (".glb", AssetKind.Model),
        (".gltf", AssetKind.Model),
        (".model", AssetKind.Model),
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

        if (fullPath.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            return GetKindBySchema(fullPath);
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

    /// <summary>The file name without its extensions: Room for Room.scene.json.</summary>
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

    /// <summary>A mood names mood.schema.json, for example; only the top-level <c>$schema</c> is read.</summary>
    private static AssetKind GetKindBySchema(string fullPath)
    {
        string? schema = ReadSchema(fullPath);
        if (schema is null || !schema.EndsWith(SchemaSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return AssetKind.Other;
        }

        string fileName = Path.GetFileName(schema);
        string kindName = fileName[..^SchemaSuffix.Length];
        if (Enum.TryParse(kindName, ignoreCase: true, out AssetKind kind))
        {
            return kind;
        }

        return AssetKind.Other;
    }

    private static string? ReadSchema(string fullPath)
    {
        byte[] bytes = File.ReadAllBytes(fullPath);
        JsonReaderOptions options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        Utf8JsonReader reader = new(bytes, options);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string property = reader.GetString()!;
                reader.Read();
                if (property == "$schema" && reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetString();
                }

                reader.Skip();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
