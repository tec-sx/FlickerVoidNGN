using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>Loads content files by their path under a root folder and keeps each loaded object until it is unloaded.</summary>
public sealed class ContentManager
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

    /// <summary>Returns the object loaded from the JSON file at <paramref name="path"/>, reading the file only the first time.</summary>
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

    /// <summary>Forgets the object loaded from <paramref name="path"/>, so the next load reads the file again.</summary>
    public bool Unload(string path)
    {
        string fullPath = GetFullPath(path);
        return _loaded.Remove(fullPath);
    }

    /// <summary>Resolves a content path against the root; paths that lead outside the root throw.</summary>
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
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
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
