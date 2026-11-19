using System.Diagnostics;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Builder.Watching;

/// <summary>
/// Watches source content folders and builds a folder again once a file in it was saved and its writes have settled, so a
/// running game sees the built file change; the build cache keeps it to the files that changed.
/// </summary>
public sealed class ContentWatchBuilder : IDisposable
{
    /// <summary>How long a folder must go without writes before it is built: editors save a file in several steps.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(100);

    private readonly AssetBuilder _builder;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<ContentBuildFolder, long> _pending = [];
    private readonly List<ContentBuildFolder> _ready = [];

    public ContentWatchBuilder(AssetBuilder builder, IReadOnlyList<ContentBuildFolder> folders)
    {
        _builder = builder;
        foreach (ContentBuildFolder folder in folders)
        {
            FileSystemWatcher watcher = new(folder.Source)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            watcher.Changed += (_, _) => Record(folder);
            watcher.Created += (_, _) => Record(folder);
            watcher.Deleted += (_, _) => Record(folder);
            watcher.Renamed += (_, _) => Record(folder);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    /// <summary>Builds each folder whose writes have settled and returns their reports; call it in a loop.</summary>
    public IReadOnlyList<BuildReport> BuildSettled()
    {
        CollectSettled();
        List<BuildReport> reports = [];
        foreach (ContentBuildFolder folder in _ready)
        {
            BuildReport report = _builder.Build(folder.Source, folder.Output);
            reports.Add(report);
        }

        return reports;
    }

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.Dispose();
        }
    }

    private void CollectSettled()
    {
        _ready.Clear();
        long now = Stopwatch.GetTimestamp();
        lock (_pending)
        {
            foreach ((ContentBuildFolder folder, long written) in _pending)
            {
                TimeSpan quiet = Stopwatch.GetElapsedTime(written, now);
                if (quiet >= SettleTime)
                {
                    _ready.Add(folder);
                }
            }

            foreach (ContentBuildFolder folder in _ready)
            {
                _pending.Remove(folder);
            }
        }
    }

    /// <summary>Notes a write from a watcher's thread.</summary>
    private void Record(ContentBuildFolder folder)
    {
        long now = Stopwatch.GetTimestamp();
        lock (_pending)
        {
            _pending[folder] = now;
        }
    }
}
