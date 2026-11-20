using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Overlays;

/// <summary>Builds a world with rendering components, seen by an editor camera looking at the origin from 10 units in a 640x480 viewport.</summary>
internal sealed class EditorIconsTestsBuilder : IDisposable
{
    public const int Width = 640;
    public const int Height = 480;

    private readonly Game _game;

    public EditorIconsTestsBuilder()
    {
        _game = new Game(NullLoggerFactory.Instance);
        _game.AddRendering();
        Camera.LookAt(Vector3.Zero, 0.3f, -0.4f, 10);
    }

    public World World => _game.World;

    public EditorCamera Camera { get; } = new();

    public Entity AddPointLight(Vector3 position, Vector3 color)
    {
        Transform transform = new() { Position = position };
        PointLight light = new() { Color = color };
        return _game.World.Create(transform, light);
    }

    public Entity AddSpotLight(Vector3 position)
    {
        Transform transform = new() { Position = position };
        return _game.World.Create(transform, new SpotLight());
    }

    public Entity AddCamera(Vector3 position)
    {
        Transform transform = new() { Position = position };
        return _game.World.Create(transform, new Camera());
    }

    public Entity AddIrradianceVolume(Vector3 position)
    {
        Transform transform = new() { Position = position };
        return _game.World.Create(transform, new IrradianceVolume());
    }

    public void Dispose()
    {
        _game.Dispose();
    }
}
