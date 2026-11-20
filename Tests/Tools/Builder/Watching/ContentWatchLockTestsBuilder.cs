using FlickerVoid.Tools.Builder.Watching;

namespace FlickerVoid.Tools.Builder.Tests.Watching;

/// <summary>Takes watch locks on a manifest in a temporary folder.</summary>
internal sealed class ContentWatchLockTestsBuilder : IDisposable
{
    private readonly string _folder;

    public ContentWatchLockTestsBuilder()
    {
        string name = Path.GetRandomFileName();
        _folder = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(_folder);
        ManifestPath = Path.Combine(_folder, ".content-build");
        File.WriteAllText(ManifestPath, "");
    }

    public string ManifestPath { get; }

    public ContentWatchLock? Build() => ContentWatchLock.TryTake(ManifestPath);

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
