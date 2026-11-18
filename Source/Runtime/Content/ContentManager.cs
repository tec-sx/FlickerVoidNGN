using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>Reads content as JSON files from disk, caching each by its full path.</summary>
public sealed class ContentManager : IContentManager
{
    private static readonly string ParentPrefix = ".." + Path.DirectorySeparatorChar;

    private readonly Dictionary<string, object> _loaded;

    public ContentManager(string rootDirectory)
    {
        string fullRoot = Path.GetFullPath(rootDirectory);
        RootDirectory = Path.TrimEndingDirectorySeparator(fullRoot);

        StringComparer comparer = StringComparer.Ordinal;
        if (OperatingSystem.IsWindows())
        {
            comparer = StringComparer.OrdinalIgnoreCase;
        }

        _loaded = new Dictionary<string, object>(comparer);
    }

    public string RootDirectory { get; }

    public T Load<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        string fullPath = GetFullPath(path);
        if (_loaded.TryGetValue(fullPath, out object? loaded))
        {
            if (loaded is T existing)
            {
                return existing;
            }

            throw new InvalidOperationException($"{path} is already loaded as {loaded.GetType().Name}, not {typeof(T).Name}.");
        }

        T content = Read(fullPath, path, typeInfo);
        _loaded.Add(fullPath, content);
        return content;
    }

    public bool Unload(string path)
    {
        string fullPath = GetFullPath(path);
        return _loaded.Remove(fullPath);
    }

    public string GetFullPath(string path)
    {
        string combined = Path.Combine(RootDirectory, path);
        string fullPath = Path.GetFullPath(combined);
        string relative = Path.GetRelativePath(RootDirectory, fullPath);
        bool outside = Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(ParentPrefix, StringComparison.Ordinal);
        if (outside)
        {
            throw new ArgumentException($"{path} is outside the content folder {RootDirectory}.", nameof(path));
        }

        return fullPath;
    }

    private static T Read<T>(string fullPath, string path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        using FileStream stream = File.OpenRead(fullPath);
        T? content;
        try
        {
            content = JsonSerializer.Deserialize(stream, typeInfo);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path}: {exception.Message}", exception);
        }

        if (content is null)
        {
            throw new InvalidDataException($"{path} holds null instead of a {typeof(T).Name}.");
        }

        return content;
    }
}
