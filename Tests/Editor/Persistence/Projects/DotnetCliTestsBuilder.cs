namespace FlickerVoid.Editor.Persistence.Tests.Projects;

/// <summary>Makes folder trees in a temporary folder for the engine lookup to walk.</summary>
internal sealed class DotnetCliTestsBuilder : IDisposable
{
    public DotnetCliTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }

    public string WithFolder(params string[] parts)
    {
        string relative = Path.Combine(parts);
        string path = Path.Combine(Folder, relative);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
