namespace FlickerVoid.Editor.Assets.Tests;

/// <summary>Builds the asset catalog over a temporary content folder.</summary>
internal sealed class AssetCatalogTestsBuilder : IDisposable
{
    private readonly string _folder;

    public AssetCatalogTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
    }

    public AssetCatalogTestsBuilder WithFile(string path, string text = "{}")
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    public AssetCatalog Build() => new(_folder);

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
