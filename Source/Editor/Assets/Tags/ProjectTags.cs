using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Editor.Core.Tags;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Assets.Tags;

/// <summary>
/// The project's tags: those declared in <see cref="FileName"/> at the content's root (tags added in the picker before anything
/// uses them) and those the content holds, counted by the types the game reads each file with. A rename or removal rewrites every
/// file of the project that holds the tag; the engine's own content is left alone.
/// </summary>
public sealed class ProjectTags(UnsavedContent content, Func<Game> game, Func<string?> busy) : ITagFiles
{
    public const string FileName = "Tags.fvtags";

    private static readonly string[] Extensions = [".fvscene", ".fventity", ".fvdata", ".fvtable", ".fvmusic", ".fvsound", ".fvflow", ".fvvehicle", ".fvbuilding", ".fvprop", ".fvfoliage", ".fvmap"];

    private TagCatalog? _catalog;

    /// <summary>Raised with the content paths a rename or removal rewrote, so whatever shows them opens them again.</summary>
    public event Action<IReadOnlyList<string>>? Rewritten;

    /// <summary>Fills the catalog, makes this its files and keeps the declared tags in step with it.</summary>
    public void Load(TagCatalog catalog)
    {
        _catalog = catalog;
        catalog.Load(ReadDeclared());
        catalog.Files = this;
        catalog.SetUses(CountUses());
        catalog.Changed += SaveDeclared;
        SaveDeclared();
    }

    /// <summary>How many times the project's files hold each tag.</summary>
    public IReadOnlyDictionary<string, int> CountUses()
    {
        ContentTags tags = new(game());
        Dictionary<string, int> uses = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in FindFiles())
        {
            byte[] json = content.Read(path);
            foreach (string tag in tags.Find(path, json))
            {
                uses[tag] = uses.GetValueOrDefault(tag) + 1;
            }
        }

        return uses;
    }

    public bool Rename(string name, string to, out string? why)
    {
        ContentTags tags = new(game());
        return Rewrite(path => tags.Rename(path, content.Read(path), name, to), out why);
    }

    public bool Remove(string name, out string? why)
    {
        ContentTags tags = new(game());
        return Rewrite(path => tags.Remove(path, content.Read(path), name), out why);
    }

    private bool Rewrite(Func<string, byte[]?> change, out string? why)
    {
        why = busy();
        if (why is not null)
        {
            return false;
        }

        List<(string Path, byte[] Json)> changed = [];
        foreach (string path in FindFiles())
        {
            if (change(path) is { } json)
            {
                changed.Add((path, json));
            }
        }

        foreach ((string path, byte[] json) in changed)
        {
            content.Write(path, json);
        }

        string[] paths = [.. changed.Select(file => file.Path)];
        Rewritten?.Invoke(paths);
        return true;
    }

    /// <summary>The project's content files that may hold tags, as content paths; the engine's folder and hidden folders are left out.</summary>
    private IEnumerable<string> FindFiles()
    {
        string root = content.RootDirectory;
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string path = Path.GetRelativePath(root, file).Replace('\\', '/');
            bool hidden = path.Split('/').Any(part => part.StartsWith('.'));
            bool engine = path.StartsWith(ContentCheck.EngineFolder + "/", StringComparison.OrdinalIgnoreCase);
            string extension = Path.GetExtension(path);
            if (!hidden && !engine && Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private IReadOnlyList<string> ReadDeclared()
    {
        string fullPath = content.GetFullPath(FileName);
        if (!File.Exists(fullPath))
        {
            return [];
        }

        try
        {
            byte[] json = content.Read(FileName);
            JsonNode? file = JsonNode.Parse(json);
            if (file?["tags"] is JsonArray tags)
            {
                return [.. tags.Select(tag => tag?.GetValue<string>() ?? "").Where(tag => tag.Length > 0)];
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }

        return [];
    }

    /// <summary>Writes the catalog's tags to the project's tags file when they differ from what it holds.</summary>
    private void SaveDeclared()
    {
        if (_catalog is null)
        {
            return;
        }

        JsonArray tags = [.. _catalog.Names.Select(name => JsonValue.Create(name))];
        JsonObject file = new() { ["tags"] = tags };
        string text = file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        byte[] json = Encoding.UTF8.GetBytes(text);
        string fullPath = content.GetFullPath(FileName);
        if (File.Exists(fullPath) && File.ReadAllBytes(fullPath).AsSpan().SequenceEqual(json))
        {
            return;
        }

        content.Write(FileName, json);
    }
}
