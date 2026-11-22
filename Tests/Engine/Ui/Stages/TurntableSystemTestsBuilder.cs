using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Ui.Stages;

namespace FlickerVoid.Engine.Ui.Tests.Stages;

/// <summary>Builds a world with one entity on a turntable and reads how far it turned about the up axis.</summary>
internal sealed class TurntableSystemTestsBuilder : IDisposable
{
    private Turntable _turntable = new();
    private Quaternion _rotation = Quaternion.Identity;

    public World World { get; } = World.Create();

    public TurntableSystemTestsBuilder WithTurntable(Turntable turntable)
    {
        _turntable = turntable;
        return this;
    }

    public TurntableSystemTestsBuilder WithRotation(Quaternion rotation)
    {
        _rotation = rotation;
        return this;
    }

    public Entity Build()
    {
        Transform transform = new() { Rotation = _rotation };
        return World.Create(transform, _turntable);
    }

    /// <summary>Degrees the entity's front (+Z) turned toward +X.</summary>
    public float Yaw(Entity entity)
    {
        Transform transform = World.Get<Transform>(entity);
        Vector3 front = Vector3.Transform(Vector3.UnitZ, transform.Rotation);
        float radians = MathF.Atan2(front.X, front.Z);
        return float.RadiansToDegrees(radians);
    }

    public void Dispose() => World.Destroy(World);
}
