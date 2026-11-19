using System.Diagnostics;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>
/// Builds the content watcher under test over a content manager on a new temporary folder, records the files the manager
/// reports as changed, and deletes the folder when disposed.
/// </summary>
internal sealed class ContentWatcherTestsBuilder : IDisposable
{
    private readonly ContentManagerTestsBuilder _content = new();
    private readonly List<string> _changed = [];
    private ContentWatcher? _watcher;

    public IReadOnlyList<string> Changed => _changed;

    public ContentWatcherTestsBuilder WithFile(string path, string text)
    {
        _content.WithFile(path, text);
        return this;
    }

    public ContentWatcher Build()
    {
        ContentManager content = _content.Build();
        content.Changed += _changed.Add;
        _watcher = new ContentWatcher(content);
        _watcher.Initialize();
        return _watcher;
    }

    public string FullPath(string path) => Path.Combine(_content.RootDirectory, path);

    public void WriteFile(string path, string text)
    {
        _content.WithFile(path, text);
    }

    public void ReplaceFile(string source, string destination)
    {
        string sourcePath = FullPath(source);
        string destinationPath = FullPath(destination);
        File.Move(sourcePath, destinationPath, overwrite: true);
    }

    /// <summary>Updates the watcher as a game would each frame until it reports a change or the time is up.</summary>
    public IReadOnlyList<string> UpdateUntilChanged(TimeSpan limit)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (_changed.Count == 0 && elapsed.Elapsed < limit)
        {
            UpdateOnce();
        }

        return _changed;
    }

    /// <summary>Updates the watcher each frame for the given time.</summary>
    public IReadOnlyList<string> UpdateFor(TimeSpan duration)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < duration)
        {
            UpdateOnce();
        }

        return _changed;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _content.Dispose();
    }

    private void UpdateOnce()
    {
        GameTime frame = new(TimeSpan.FromMilliseconds(16), TimeSpan.Zero, 0);
        _watcher!.Update(in frame);
        Thread.Sleep(5);
    }
}
