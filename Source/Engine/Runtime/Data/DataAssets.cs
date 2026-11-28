using System.Text.Json;
using System.Text.Json.Nodes;

using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Data;

/// <summary>
/// Reads data asset (<c>.fvdata</c>) and data table (<c>.fvtable</c>) files into the types registered in <see cref="DataTypes"/>,
/// keeping one object per file until the file changes on disk.
/// </summary>
public sealed class DataAssets : IDisposable
{
    public const string AssetExtension = ".fvdata";

    public const string TableExtension = ".fvtable";

    private readonly IContentManager _content;
    private readonly DataTypes _types;
    private readonly Dictionary<string, object> _loaded = [];
    private List<string>? _files;

    public DataAssets(IContentManager content, DataTypes types)
    {
        _content = content;
        _types = types;
        _content.Changed += HandleChanged;
    }

    /// <summary>The data asset at <paramref name="path"/>; throws <see cref="InvalidDataException"/> when the file holds another type or can't be read.</summary>
    public T Load<T>(string path)
        where T : class
    {
        object asset = GetOrRead(path, ReadAsset);
        if (asset is not T typed)
        {
            throw new InvalidDataException($"{path} holds a {asset.GetType().Name}, not a {typeof(T).Name}.");
        }

        return typed;
    }

    /// <summary>The data table at <paramref name="path"/>; throws <see cref="InvalidDataException"/> when its rows are of another type or it can't be read.</summary>
    public DataTable<TRow> LoadTable<TRow>(string path)
        where TRow : class
    {
        object table = GetOrRead(path, ReadTable<TRow>);
        if (table is not DataTable<TRow> typed)
        {
            throw new InvalidDataException($"{path} holds rows of another type, not {typeof(TRow).Name}.");
        }

        return typed;
    }

    /// <summary>The paths of the content folder's data asset files, as content paths with forward slashes, hidden folders skipped.</summary>
    public IReadOnlyList<string> FindFiles()
    {
        if (_files is not null)
        {
            return _files;
        }

        List<string> files = [];
        string root = _content.RootDirectory;
        if (Directory.Exists(root))
        {
            foreach (string fullPath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!_types.IsAssetFile(fullPath))
                {
                    continue;
                }

                string path = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
                if (!path.Split('/').Any(static part => part.StartsWith('.')))
                {
                    files.Add(path);
                }
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        _files = files;
        return files;
    }

    /// <summary>Every data asset of type <typeparamref name="T"/> in the content folder, such as all item definitions.</summary>
    public IReadOnlyList<T> LoadAll<T>()
        where T : class
    {
        List<T> found = [];
        foreach (string path in FindFiles())
        {
            if (TryLoad(path) is T asset)
            {
                found.Add(asset);
            }
        }

        return found;
    }

    /// <summary>The content path of a data asset this loaded and still holds, such as an item a save names by its file.</summary>
    public bool TryGetPath(object asset, out string path)
    {
        foreach ((string fullPath, object loaded) in _loaded)
        {
            if (ReferenceEquals(loaded, asset))
            {
                string relative = Path.GetRelativePath(_content.RootDirectory, fullPath);
                path = relative.Replace('\\', '/');
                return true;
            }
        }

        path = string.Empty;
        return false;
    }

    /// <summary>The definition of type <typeparamref name="T"/> whose id is <paramref name="id"/>; null when no file holds one.</summary>
    public T? Find<T>(Tag id)
        where T : Definition
    {
        foreach (T definition in LoadAll<T>())
        {
            if (definition.Id == id)
            {
                return definition;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
    }

    private object GetOrRead(string path, Func<string, JsonObject, object> read)
    {
        string fullPath = _content.GetFullPath(path);
        if (_loaded.TryGetValue(fullPath, out object? loaded))
        {
            return loaded;
        }

        JsonObject file = _content.Load(path, RuntimeJsonContext.Default.JsonObject);
        object value;
        try
        {
            value = read(path, file);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"{path} can't be read: {exception.Message}", exception);
        }

        _loaded.Add(fullPath, value);
        return value;
    }

    private object ReadAsset(string path, JsonObject file)
    {
        return _types.ReadAsset(path, file);
    }

    private object ReadTable<TRow>(string path, JsonObject file)
        where TRow : class
    {
        List<KeyValuePair<string, object>> read = _types.ReadRows(path, file, out Type type);
        if (type != typeof(TRow))
        {
            throw new InvalidDataException($"{path} holds rows of {type.Name}, not {typeof(TRow).Name}.");
        }

        Dictionary<string, TRow> rows = [];
        foreach ((string name, object row) in read)
        {
            rows.Add(name, (TRow)row);
        }

        return new DataTable<TRow>(rows);
    }

    /// <summary>The asset at the path, or null when the file can't be read, so one broken file doesn't hide the others.</summary>
    private object? TryLoad(string path)
    {
        try
        {
            return GetOrRead(path, ReadAsset);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    private void HandleChanged(string fullPath)
    {
        _loaded.Remove(fullPath);
        if (_types.IsAssetFile(fullPath))
        {
            _files = null;
        }
    }
}
