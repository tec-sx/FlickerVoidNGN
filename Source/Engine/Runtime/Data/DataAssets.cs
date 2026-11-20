using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

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
        Type type = DataTypes.GetType(path, file, _types.RowTypes);
        if (type != typeof(TRow))
        {
            throw new InvalidDataException($"{path} holds rows of {type.Name}, not {typeof(TRow).Name}.");
        }

        JsonTypeInfo<TRow> typeInfo = (JsonTypeInfo<TRow>)_types.Options.GetTypeInfo(type);
        Dictionary<string, TRow> rows = [];
        if (file["rows"] is JsonObject fileRows)
        {
            foreach ((string name, JsonNode? value) in fileRows)
            {
                TRow? row = value.Deserialize(typeInfo);
                rows.Add(name, row ?? throw new InvalidDataException($"{path} has an empty row {name}."));
            }
        }

        return new DataTable<TRow>(rows);
    }

    private void HandleChanged(string fullPath)
    {
        _loaded.Remove(fullPath);
    }
}
