using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Interface.Hud.Minimap;

using World = Arch.Core.World;

namespace FlickerVoid.Interface.Tests.Hud.Minimap;

/// <summary>Builds a minimap over a world with a camera and, optionally, the protagonist, each turned about the vertical axis.</summary>
internal sealed class MinimapViewModelTestsBuilder : IDisposable
{
    private readonly World _world = World.Create();

    public MinimapViewModelTestsBuilder WithCamera(float yawDegrees)
    {
        Quaternion yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(yawDegrees));
        Quaternion pitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-30));
        Transform transform = new() { Position = new Vector3(0, 6, 9), Rotation = yaw * pitch };
        _world.Create(transform, new Camera());
        return this;
    }

    public MinimapViewModelTestsBuilder WithProtagonist(Vector3 position, float yawDegrees)
    {
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(yawDegrees));
        Transform transform = new() { Position = position, Rotation = rotation };
        _world.Create(transform, new Gameplay.Protagonist.Protagonist());
        return this;
    }

    public MinimapViewModel Build() => new(_world);

    public void Dispose() => World.Destroy(_world);
}
