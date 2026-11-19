namespace FlickerVoid.Engine.Builder.Tests;

/// <summary>
/// Builds the asset builder under test with a <see cref="TestImporter"/>, over new temporary source and output folders that
/// are deleted when disposed.
/// </summary>
internal sealed class AssetBuilderTestsBuilder : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FlickerVoidBuilderTests", Guid.NewGuid().ToString("N"));

    public AssetBuilderTestsBuilder()
    {
        SourceDirectory = Path.Combine(_root, "Content");
        OutputDirectory = Path.Combine(_root, "Built");
        Directory.CreateDirectory(SourceDirectory);
    }

    public string SourceDirectory { get; }

    public string OutputDirectory { get; }

    public TestImporter Importer { get; } = new();

    public AssetBuilderTestsBuilder WithSource(string path, string text)
    {
        WriteSource(path, text);
        return this;
    }

    public AssetBuilder Build() => new([Importer]);

    public BuildReport BuildAll(AssetBuilder builder) => builder.Build(SourceDirectory, OutputDirectory);

    public void WriteSource(string path, string text)
    {
        string fullPath = Path.Combine(SourceDirectory, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
    }

    public void DeleteSource(string path)
    {
        string fullPath = Path.Combine(SourceDirectory, path);
        File.Delete(fullPath);
    }

    public bool OutputExists(string path)
    {
        string fullPath = Path.Combine(OutputDirectory, path);
        return File.Exists(fullPath);
    }

    public string ReadOutput(string path)
    {
        string fullPath = Path.Combine(OutputDirectory, path);
        return File.ReadAllText(fullPath);
    }

    public void DeleteOutput(string path)
    {
        string fullPath = Path.Combine(OutputDirectory, path);
        File.Delete(fullPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
