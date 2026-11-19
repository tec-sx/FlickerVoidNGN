namespace FlickerVoid.Engine.Shaders.Tests;

/// <summary>A new folder under the temporary path for a test's files, deleted when disposed.</summary>
internal sealed class TemporaryFolder : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FlickerVoidShaderTests", Guid.NewGuid().ToString("N"));

    public string FullPath(string path) => Path.Combine(_root, path);

    public void Write(string path, string text)
    {
        string fullPath = FullPath(path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
