using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Rendering.Materials;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

/// <summary>Builds the material editor over an empty scene in a temporary content folder, with a substitute preview.</summary>
internal sealed class MaterialEditorViewModelTestsBuilder : IDisposable
{
    private readonly MaterialBrowserTestsBuilder _files = new();
    private EditorScene? _scene;

    public IMaterialPreview Preview { get; } = Substitute.For<IMaterialPreview>();

    public EditorScene Scene => _scene!;

    public string Folder => _files.Folder;

    public MaterialEditorViewModelTestsBuilder WithMaterial(string path, string text)
    {
        _files.WithFile(path, text);
        return this;
    }

    public MaterialEditorViewModel Build()
    {
        _scene = new EditorScene(NullLoggerFactory.Instance, _files.Folder);
        Preview.Render(Arg.Any<MaterialFile>(), Arg.Any<PreviewShape>(), Arg.Any<float>(), Arg.Any<int>()).Returns(new byte[4]);
        return new MaterialEditorViewModel(_scene, Preview);
    }

    public string ReadMaterial(string path) => File.ReadAllText(Path.Combine(_files.Folder, path));

    public void Dispose()
    {
        _scene?.Dispose();
        _files.Dispose();
    }
}
