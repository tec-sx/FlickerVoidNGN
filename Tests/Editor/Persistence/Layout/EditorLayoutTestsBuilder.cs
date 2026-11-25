using FlickerVoid.Editor.Persistence.Layout;

namespace FlickerVoid.Editor.Persistence.Tests.Layout;

/// <summary>Builds the editor's layout over a file in a temporary folder.</summary>
internal sealed class EditorLayoutTestsBuilder : IDisposable
{
    private readonly string _folder;

    public EditorLayoutTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
    }

    private string File => Path.Combine(_folder, "Settings", "Layout.json");

    public EditorLayoutTestsBuilder WithBrokenFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, "{ not json");
        return this;
    }

    public EditorLayout Build() => new(File);

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
