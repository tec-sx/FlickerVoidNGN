using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>Builds the content manager under test on a new temporary folder, which it deletes when disposed.</summary>
internal sealed class ContentManagerTestsBuilder : IDisposable
{
    public ContentManagerTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        RootDirectory = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public ContentManagerTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(RootDirectory, path);
        string? folder = Path.GetDirectoryName(fullPath);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(fullPath, json);
        return this;
    }

    public ContentManager Build() => new(RootDirectory);

    public void Dispose()
    {
        Directory.Delete(RootDirectory, recursive: true);
    }
}
