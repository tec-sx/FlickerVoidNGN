using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Tests.Materials;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Engine.Rendering.Materials;

using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace FlickerVoid.Editor.Tests.Materials;

/// <summary>Builds the material editor over an empty scene in a temporary content folder, with a substitute preview.</summary>
internal sealed class MaterialEditorViewModelTestsBuilder : IDisposable
{
    private readonly MaterialBrowserTestsBuilder _files = new();
    private EditorScene? _scene;

    public IMaterialPreview Preview { get; } = Substitute.For<IMaterialPreview>();

    public EditorScene Scene => _scene!;

    public UnsavedContent Content { get; private set; } = null!;

    public string Folder => _files.Folder;

    public MaterialEditorViewModelTestsBuilder WithMaterial(string path, string text)
    {
        _files.WithFile(path, text);
        return this;
    }

    public MaterialEditorViewModel Build()
    {
        Content = new UnsavedContent(new ContentManager(_files.Folder));
        _scene = new EditorScene(NullLoggerFactory.Instance, Content);
        Preview.Render(Arg.Any<MaterialFile>(), Arg.Any<PreviewShape>(), Arg.Any<float>(), Arg.Any<int>()).Returns(new byte[4]);
        return new MaterialEditorViewModel(Content, Preview);
    }

    public string ReadMaterial(string path) => File.ReadAllText(Path.Combine(_files.Folder, path));

    public void Dispose()
    {
        _scene?.Dispose();
        _files.Dispose();
    }
}
