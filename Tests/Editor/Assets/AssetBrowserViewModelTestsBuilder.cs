using FlickerVoid.Engine.Editor.Assets;

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

    public AssetBrowserViewModel Build() => new(new AssetCatalog(_folder));

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
