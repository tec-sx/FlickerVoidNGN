using FlickerVoid.Engine.Editor.Assets;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Tests.Assets;

/// <summary>Builds the asset browser over a temporary content folder.</summary>
internal sealed class AssetBrowserViewModelTestsBuilder : IDisposable
{
    private readonly string _folder;

    public AssetBrowserViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
    }

    public AssetBrowserViewModelTestsBuilder WithFile(string path, string text = "{}")
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    /// <summary>A file in a source folder that the content folder's build manifest says is built into the output folder.</summary>
    public AssetBrowserViewModelTestsBuilder WithSourceFile(string outputFolder, string path)
    {
        string source = Path.Combine(_folder, "_Source");
        string fullPath = Path.Combine(source, path);
        Directory.CreateDirectory(source);
        File.WriteAllText(fullPath, "{}");
        string manifest = Path.Combine(_folder, ContentBuildManifest.FileName);
        File.WriteAllLines(manifest, [$"folder={source}|{outputFolder}"]);
        return this;
    }

    public AssetBrowserViewModel Build() => new(new ContentManager(_folder));

    public string ReadFile(string path) => File.ReadAllText(Path.Combine(_folder, path));

    public bool Exists(string path) => File.Exists(Path.Combine(_folder, path));

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
