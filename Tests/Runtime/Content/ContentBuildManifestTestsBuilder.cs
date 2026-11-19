using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>Writes a content build manifest into a new temporary content folder, deleted when disposed.</summary>
internal sealed class ContentBuildManifestTestsBuilder : IDisposable
{
    public ContentBuildManifestTestsBuilder()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "FlickerVoidManifestTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public ContentBuildManifestTestsBuilder WithLines(params string[] lines)
    {
        string path = Path.Combine(RootDirectory, ContentBuildManifest.FileName);
        File.WriteAllLines(path, lines);
        return this;
    }

    public ContentBuildManifest? Find() => ContentBuildManifest.Find(RootDirectory);

    public string FullPath(string path) => Path.GetFullPath(path, RootDirectory);

    public void Dispose()
    {
        Directory.Delete(RootDirectory, recursive: true);
    }
}
