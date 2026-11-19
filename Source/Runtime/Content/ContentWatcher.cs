using System.Diagnostics;

using Arch.System;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// Watches the content folder for files that change on disk and reports each one to the content manager once per frame, on
/// the game thread, after its writes have settled and it can be read.
/// </summary>
public sealed class ContentWatcher : ISystem<GameTime>
{
    /// <summary>How long a file must go without writes before it counts as saved: editors write a file in several steps.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(100);

    private readonly IContentManager _content;
    private readonly Dictionary<string, long> _pending = [];
    private readonly List<string> _ready = [];
    private FileSystemWatcher? _watcher;

    public ContentWatcher(IContentManager content)
    {
        _content = content;
    }

    public void Initialize()
    {
        _watcher = new FileSystemWatcher(_content.RootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        _watcher.Changed += HandleChanged;
        _watcher.Created += HandleChanged;
        _watcher.Renamed += HandleRenamed;
        _watcher.EnableRaisingEvents = true;
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        CollectSettled();
        foreach (string path in _ready)
        {
            _content.NotifyChanged(path);
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }

    private static bool CanRead(string path)
    {
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Moves the files whose last write is older than the settle time and that can be opened into the ready list; files still locked wait for a later frame.</summary>
    private void CollectSettled()
    {
        _ready.Clear();
        long now = Stopwatch.GetTimestamp();
        lock (_pending)
        {
            foreach ((string path, long written) in _pending)
            {
                TimeSpan quiet = Stopwatch.GetElapsedTime(written, now);
                if (quiet >= SettleTime && CanRead(path))
                {
                    _ready.Add(path);
                }
            }

            foreach (string path in _ready)
            {
                _pending.Remove(path);
            }
        }
    }

    private void HandleChanged(object sender, FileSystemEventArgs e)
    {
        Record(e.FullPath);
    }

    private void HandleRenamed(object sender, RenamedEventArgs e)
    {
        Record(e.FullPath);
    }

    /// <summary>Notes a write from the watcher's thread; folders are skipped.</summary>
    private void Record(string path)
    {
        if (Directory.Exists(path))
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        lock (_pending)
        {
            _pending[path] = now;
        }
    }
}
