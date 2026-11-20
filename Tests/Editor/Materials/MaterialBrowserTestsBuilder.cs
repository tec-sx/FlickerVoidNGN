using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

/// <summary>Builds the material browser over a temporary content folder.</summary>
internal sealed class MaterialBrowserTestsBuilder : IDisposable
{
    public MaterialBrowserTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }

    public MaterialBrowserTestsBuilder WithFile(string path, string text = """{ "parameters": {} }""")
    {
        string fullPath = Path.Combine(Folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    public MaterialBrowser Build() => new(new ContentManager(Folder));

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
