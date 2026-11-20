using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Tests;

/// <summary>Builds the editor's content with unsaved edits over a temporary content folder.</summary>
internal sealed class UnsavedContentTestsBuilder : IDisposable
{
    public UnsavedContentTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
        Disk = new ContentManager(Folder);
    }

    public string Folder { get; }

    public ContentManager Disk { get; }

    public UnsavedContentTestsBuilder WithFile(string path, string text)
    {
        string fullPath = Path.Combine(Folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
        return this;
    }

    public UnsavedContent Build() => new(Disk);

    public string ReadFile(string path)
    {
        string fullPath = Path.Combine(Folder, path);
        return File.ReadAllText(fullPath);
    }

    /// <summary>Changes the file as another program would, then reports it as the content watcher does.</summary>
    public void ChangeOnDisk(string path, string text)
    {
        WithFile(path, text);
        Disk.NotifyChanged(path);
    }

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
