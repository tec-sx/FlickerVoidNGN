using System.Text.Json;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Tests.Materials;

/// <summary>Builds materials and masters in a temporary content folder, served unsaved edits included, to edit and trace their lineage.</summary>
internal sealed class MaterialLineageTestsBuilder : IDisposable
{
    private readonly MaterialBrowserTestsBuilder _files = new();
    private UnsavedContent? _content;

    public UnsavedContent Content => _content ??= new UnsavedContent(new ContentManager(_files.Folder));

    public MaterialLineageTestsBuilder WithFile(string path, string text)
    {
        _files.WithFile(path, text);
        return this;
    }

    public static MaterialFile Material(string json)
    {
        return JsonSerializer.Deserialize(json, RenderingJsonContext.Default.MaterialFile)!;
    }

    public MaterialFile Load(string path) => Content.Load(path, RenderingJsonContext.Default.MaterialFile);

    public MaterialDocument Open(string path) => new(Content, path);

    public string Read(string path) => File.ReadAllText(Path.Combine(_files.Folder, path));

    public void Dispose()
    {
        _files.Dispose();
    }
}
