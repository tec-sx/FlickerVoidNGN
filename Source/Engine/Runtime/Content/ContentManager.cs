using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>Reads content as JSON files from disk, caching each by its full path.</summary>
public sealed class ContentManager : IContentManager
{
    private static readonly string ParentPrefix = ".." + Path.DirectorySeparatorChar;

    private readonly Dictionary<string, object> _loaded;
    private readonly ContentBuildManifest? _manifest;

    public ContentManager(string rootDirectory)
    {
        string fullRoot = Path.GetFullPath(rootDirectory);
        RootDirectory = Path.TrimEndingDirectorySeparator(fullRoot);

        _loaded = new Dictionary<string, object>(PathComparer);
        _manifest = ContentBuildManifest.Find(RootDirectory);
    }

    /// <summary>Compares full paths as the file system does: ignoring case on Windows.</summary>
    public static StringComparer PathComparer { get; } = CreatePathComparer();

    public event Action<string>? Changed;

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

    public void NotifyChanged(string path)
    {
        string fullPath = GetFullPath(path);
        _loaded.Remove(fullPath);
        Changed?.Invoke(fullPath);
    }

    public void Write(string path, byte[] contents)
    {
        string fullPath = GetFullPath(path);
        string? sourcePath = FindSourcePath(fullPath);
        if (sourcePath is not null)
        {
            WriteFile(sourcePath, contents);
        }

        WriteFile(fullPath, contents);
        NotifyChanged(path);
    }

    public void CreateFolder(string path)
    {
        string fullPath = GetFullPath(path);
        string? sourcePath = FindSourcePath(fullPath);
        if (sourcePath is not null)
        {
            Directory.CreateDirectory(sourcePath);
        }

        Directory.CreateDirectory(fullPath);
    }

    public void Delete(string path)
    {
        string fullPath = GetFullPath(path);
        string? sourcePath = FindSourcePath(fullPath);
        List<string> files = ListFiles(fullPath);
        if (sourcePath is not null)
        {
            DeleteEntry(sourcePath);
        }

        DeleteEntry(fullPath);
        ReportChanged(files);
    }

    public void Move(string path, string newPath)
    {
        string fullPath = GetFullPath(path);
        string newFullPath = GetFullPath(newPath);
        string? sourcePath = FindSourcePath(fullPath);
        string? newSourcePath = FindSourcePath(newFullPath);
        List<string> files = ListFiles(fullPath);
        if (sourcePath is not null && newSourcePath is not null)
        {
            MoveEntry(sourcePath, newSourcePath);
        }

        MoveEntry(fullPath, newFullPath);
        List<string> moved = ListFiles(newFullPath);
        ReportChanged(files);
        ReportChanged(moved);
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

    /// <summary>The file, or every file in the folder, at the full path; none when nothing is there.</summary>
    private static List<string> ListFiles(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return [fullPath];
        }

        if (Directory.Exists(fullPath))
        {
            return [.. Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)];
        }

        return [];
    }

    private void ReportChanged(List<string> fullPaths)
    {
        foreach (string fullPath in fullPaths)
        {
            _loaded.Remove(fullPath);
            Changed?.Invoke(fullPath);
        }
    }

    private static void DeleteEntry(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static void MoveEntry(string fullPath, string newFullPath)
    {
        string? folder = Path.GetDirectoryName(newFullPath);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        if (File.Exists(fullPath))
        {
            File.Move(fullPath, newFullPath);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Move(fullPath, newFullPath);
        }
    }

    private static void WriteFile(string fullPath, byte[] contents)
    {
        string? folder = Path.GetDirectoryName(fullPath);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(fullPath, contents);
    }

    public string GetSourcePath(string path)
    {
        string fullPath = GetFullPath(path);
        return FindSourcePath(fullPath) ?? fullPath;
    }

    /// <summary>The source file a built file comes from, when the build manifest names a game source folder that holds it.</summary>
    private string? FindSourcePath(string fullPath)
    {
        if (_manifest is null)
        {
            return null;
        }

        foreach (ContentBuildFolder folder in _manifest.Folders)
        {
            if (folder.Engine)
            {
                continue;
            }

            string relative = Path.GetRelativePath(folder.Output, fullPath);
            bool outside = Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(ParentPrefix, StringComparison.Ordinal);
            if (!outside)
            {
                return Path.Combine(folder.Source, relative);
            }
        }

        return null;
    }

    private static StringComparer CreatePathComparer()
    {
        if (OperatingSystem.IsWindows())
        {
            return StringComparer.OrdinalIgnoreCase;
        }

        return StringComparer.Ordinal;
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
