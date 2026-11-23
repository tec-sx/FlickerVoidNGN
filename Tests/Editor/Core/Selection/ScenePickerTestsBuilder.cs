using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Selection;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Selection;

/// <summary>Builds a world with rendering components and a picker over a temporary content folder of baked models.</summary>
internal sealed class ScenePickerTestsBuilder : IDisposable
{
    private readonly string _contentFolder;
    private readonly Game _game;
    private ContentManager? _content;
    private ScenePicker? _picker;

    public ScenePickerTestsBuilder()
    {
        _game = new Game(NullLoggerFactory.Instance);
        _game.AddRendering();
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
    }

    public World World => _game.World;

    /// <summary>Writes a baked model named <paramref name="path"/> holding a square of the given half size facing +Z, centred on its origin.</summary>
    public ScenePickerTestsBuilder WithSquare(string path, float halfSize)
    {
        MeshVertex[] vertices =
        [
            new(new Vector3(-halfSize, -halfSize, 0), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(halfSize, -halfSize, 0), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(halfSize, halfSize, 0), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(-halfSize, halfSize, 0), Vector3.UnitZ, Vector2.Zero),
        ];
        uint[] indices = [0, 1, 2, 0, 2, 3];
        MaterialData material = new(Vector4.One, null);
        MeshPart part = new(vertices, indices, material);
        ModelData model = new([part], []);
        string builtPath = Path.ChangeExtension(path, ".fvmodel");
        string fullPath = Path.Combine(_contentFolder, builtPath);
        using FileStream stream = File.Create(fullPath);
        ModelFile.Write(model, stream);
        return this;
    }

    public Entity AddMesh(string mesh, Vector3 position, float scale = 1)
    {
        Transform transform = new() { Position = position, Scale = new Vector3(scale) };
        MeshRenderer renderer = new() { Mesh = mesh };
        return _game.World.Create(transform, renderer);
    }

    public Entity AddGreybox(Greybox greybox, Vector3 position)
    {
        Transform transform = new() { Position = position };
        return _game.World.Create(transform, greybox);
    }

    /// <summary>Reports a content file changed, as the content watcher would.</summary>
    public void NotifyChanged(string path)
    {
        _content!.NotifyChanged(path);
    }

    public ScenePicker Build()
    {
        _content = new ContentManager(_contentFolder);
        _picker = new ScenePicker(_content);
        return _picker;
    }

    public void Dispose()
    {
        _picker?.Dispose();
        _game.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }
}
