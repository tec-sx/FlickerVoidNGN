using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests.Viewports;

/// <summary>Builds the scene view over an empty scene in a temporary content folder, shown in a 640x480 viewport.</summary>
internal sealed class ViewportViewModelTestsBuilder : IDisposable
{
    public const int Width = 640;
    public const int Height = 480;

    private readonly string _contentFolder;
    private readonly EditorScene _scene;

    public ViewportViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        _scene = new EditorScene(NullLoggerFactory.Instance, new ContentManager(_contentFolder));
        ViewModel = new ViewportViewModel(_scene, validation: false);
    }

    public ViewportViewModel ViewModel { get; }

    public World World => _scene.Game.World;

    public Entity AddPointLight(Vector3 position)
    {
        Transform transform = new() { Position = position };
        return World.Create(transform, new PointLight());
    }

    /// <summary>Opens a scene file holding one entity, Lamp, and returns it.</summary>
    public Entity OpenLampScene()
    {
        string fullPath = Path.Combine(_contentFolder, "Room.fvscene");
        File.WriteAllText(fullPath, """{ "entities": [ { "name": "Lamp", "components": { "transform": {}, "pointLight": {} } } ] }""");
        _scene.Open("Room.fvscene");
        return _scene.Entities[0];
    }

    /// <summary>The pixel a world point shows at.</summary>
    public Vector2 PixelOf(Vector3 point)
    {
        return ViewModel.Camera.Project(point, Width, Height);
    }

    public void Click(Vector2 pixel)
    {
        ViewModel.PointerPressed(pixel, Width, Height);
        ViewModel.PointerReleased();
    }

    public void Dispose()
    {
        _scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }
}
