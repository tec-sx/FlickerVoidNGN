using System.Diagnostics;

using FlickerVoid.Tools.Builder.Watching;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Tools.Builder.Tests.Watching;

/// <summary>
/// Builds a source folder once, as the game's build does, then the watch builder under test over it, in new temporary
/// folders that are deleted when disposed.
/// </summary>
internal sealed class ContentWatchBuilderTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();
    private readonly List<BuildReport> _reports = [];
    private ContentWatchBuilder? _watcher;

    public TestImporter Importer => _files.Importer;

    public ContentWatchBuilderTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public ContentWatchBuilder Build()
    {
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        ContentBuildFolder folder = new(_files.SourceDirectory, _files.OutputDirectory);
        _watcher = new ContentWatchBuilder(builder, [folder]);
        return _watcher;
    }

    public void WriteSource(string path, string text)
    {
        _files.WriteSource(path, text);
    }

    public string ReadOutput(string path) => _files.ReadOutput(path);

    /// <summary>Updates the watch builder as its loop does until it builds a folder or the time is up.</summary>
    public bool UpdateUntilBuilt(TimeSpan limit)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (_reports.Count == 0 && elapsed.Elapsed < limit)
        {
            IReadOnlyList<BuildReport> reports = _watcher!.BuildSettled();
            _reports.AddRange(reports);
            Thread.Sleep(10);
        }

        return _reports.Count > 0;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _files.Dispose();
    }
}
