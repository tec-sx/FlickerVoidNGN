using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// The editor's content: the files on disk, with the material and mood files edited but not saved held in memory, so the
/// scene shows the edits at once and the file changes only when they are saved.
/// </summary>
public sealed class UnsavedContent : IContentManager
{
    private static readonly string[] TrackedExtensions = [".fvmat", ".fvmood"];

    private readonly IContentManager _disk;
    private readonly Dictionary<string, byte[]> _unsaved = new(ContentManager.PathComparer);
    private readonly Dictionary<string, object> _loaded = new(ContentManager.PathComparer);
    private readonly Dictionary<string, byte[]> _saved = new(ContentManager.PathComparer);

    public UnsavedContent(IContentManager disk)
    {
        _disk = disk;
        _disk.Changed += HandleDiskChanged;
    }

    public event Action<string>? Changed;

    /// <summary>Raised when a file gets unsaved edits or loses them, by saving or discarding.</summary>
    public event Action? UnsavedChanged;

    /// <summary>Raised with a file's full path when it changed on disk while it has unsaved edits, which are kept.</summary>
    public event Action<string>? ChangedOnDisk;

    public string RootDirectory => _disk.RootDirectory;

    public bool HasUnsaved => _unsaved.Count > 0;

    /// <summary>The files with unsaved edits, as content paths, sorted.</summary>
    public IReadOnlyList<string> UnsavedPaths
    {
        get
        {
            List<string> paths = [];
            foreach (string fullPath in _unsaved.Keys)
            {
                string path = Path.GetRelativePath(RootDirectory, fullPath);
                paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }
    }

    /// <summary>Whether the file at the content path holds edits that are not saved.</summary>
    public bool IsUnsaved(string path)
    {
        string fullPath = GetFullPath(path);
        return _unsaved.ContainsKey(fullPath);
    }

    /// <summary>The file's contents: its unsaved edits, or the file on disk.</summary>
    public byte[] Read(string path)
    {
        string fullPath = GetFullPath(path);
        if (_unsaved.TryGetValue(fullPath, out byte[]? contents))
        {
            return contents;
        }

        return File.ReadAllBytes(fullPath);
    }

    /// <summary>Holds new contents for a material or mood file without saving them, and reports the file changed.</summary>
    public void Edit(string path, byte[] contents)
    {
        string fullPath = GetFullPath(path);
        bool tracked = TrackedExtensions.Any(extension => fullPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        if (!tracked)
        {
            throw new ArgumentException($"{path} is not a material or a mood file, whose edits are kept unsaved.", nameof(path));
        }

        bool added = _unsaved.TryAdd(fullPath, contents);
        if (!added)
        {
            _unsaved[fullPath] = contents;
        }

        _loaded.Remove(fullPath);
        Changed?.Invoke(fullPath);
        if (added)
        {
            UnsavedChanged?.Invoke();
        }
    }

    /// <summary>Writes the file's unsaved edits to disk; the change the write causes is not reported again.</summary>
    public void Save(string path)
    {
        string fullPath = GetFullPath(path);
        if (!_unsaved.Remove(fullPath, out byte[]? contents))
        {
            return;
        }

        _loaded.Remove(fullPath);
        _saved[fullPath] = contents;
        _disk.Write(path, contents);
        UnsavedChanged?.Invoke();
    }

    public void SaveAll()
    {
        foreach (string path in UnsavedPaths)
        {
            Save(path);
        }
    }

    /// <summary>Forgets the file's unsaved edits, going back to the file on disk, and reports the file changed.</summary>
    public void Discard(string path)
    {
        string fullPath = GetFullPath(path);
        if (!_unsaved.Remove(fullPath))
        {
            return;
        }

        _loaded.Remove(fullPath);
        _disk.Unload(path);
        Changed?.Invoke(fullPath);
        UnsavedChanged?.Invoke();
    }

    public void DiscardAll()
    {
        foreach (string path in UnsavedPaths)
        {
            Discard(path);
        }
    }

    public T Load<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        string fullPath = GetFullPath(path);
        if (!_unsaved.TryGetValue(fullPath, out byte[]? contents))
        {
            return _disk.Load(path, typeInfo);
        }

        if (_loaded.TryGetValue(fullPath, out object? loaded))
        {
            if (loaded is T existing)
            {
                return existing;
            }

            throw new InvalidOperationException($"{path} is already loaded as {loaded.GetType().Name}, not {typeof(T).Name}.");
        }

        T content = Deserialize(path, contents, typeInfo);
        _loaded.Add(fullPath, content);
        return content;
    }

    public bool Unload(string path)
    {
        string fullPath = GetFullPath(path);
        bool forgotten = _loaded.Remove(fullPath);
        bool unloaded = _disk.Unload(path);
        return forgotten || unloaded;
    }

    public void NotifyChanged(string path) => _disk.NotifyChanged(path);

    /// <summary>Saves the file at once, in place of any unsaved edits.</summary>
    public void Write(string path, byte[] contents)
    {
        string fullPath = GetFullPath(path);
        bool dropped = DropUnsaved(fullPath);
        _disk.Write(path, contents);
        if (dropped)
        {
            UnsavedChanged?.Invoke();
        }
    }

    public void CreateFolder(string path) => _disk.CreateFolder(path);

    public void Delete(string path)
    {
        string fullPath = GetFullPath(path);
        bool dropped = DropUnsaved(fullPath);
        _disk.Delete(path);
        if (dropped)
        {
            UnsavedChanged?.Invoke();
        }
    }

    public void Move(string path, string newPath)
    {
        string fullPath = GetFullPath(path);
        bool dropped = DropUnsaved(fullPath);
        _disk.Move(path, newPath);
        if (dropped)
        {
            UnsavedChanged?.Invoke();
        }
    }

    public string GetFullPath(string path) => _disk.GetFullPath(path);

    private static T Deserialize<T>(string path, byte[] contents, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        T? content;
        try
        {
            content = JsonSerializer.Deserialize(contents, typeInfo);
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

    private static bool HoldsOnDisk(string fullPath, byte[] contents)
    {
        try
        {
            byte[] onDisk = File.ReadAllBytes(fullPath);
            return onDisk.AsSpan().SequenceEqual(contents);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Forgets the unsaved edits of the file at the full path, or of every file in the folder there.</summary>
    private bool DropUnsaved(string fullPath)
    {
        string folder = Path.TrimEndingDirectorySeparator(fullPath) + Path.DirectorySeparatorChar;
        List<string> dropped = [];
        foreach (string unsaved in _unsaved.Keys)
        {
            bool inside = ContentManager.PathComparer.Equals(unsaved, fullPath)
                || unsaved.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
            if (inside)
            {
                dropped.Add(unsaved);
            }
        }

        foreach (string unsaved in dropped)
        {
            _unsaved.Remove(unsaved);
            _loaded.Remove(unsaved);
            _saved.Remove(unsaved);
        }

        return dropped.Count > 0;
    }

    private void HandleDiskChanged(string fullPath)
    {
        if (_saved.TryGetValue(fullPath, out byte[]? saved))
        {
            if (HoldsOnDisk(fullPath, saved))
            {
                return;
            }

            _saved.Remove(fullPath);
        }

        if (_unsaved.ContainsKey(fullPath))
        {
            ChangedOnDisk?.Invoke(fullPath);
            return;
        }

        _loaded.Remove(fullPath);
        Changed?.Invoke(fullPath);
    }
}
