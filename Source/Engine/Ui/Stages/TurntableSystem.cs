using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Ui.Stages;

/// <summary>Spins the turntables of a world each frame; stages spin and drag theirs through the same rules.</summary>
public sealed class TurntableSystem(World world) : BaseSystem<World, GameTime>(world)
{
    private static readonly QueryDescription Turntables = new QueryDescription().WithAll<Transform, Turntable>();

    public static void Spin(World world, float seconds)
    {
        world.Query(in Turntables, (ref Transform transform, ref Turntable turntable) =>
        {
            transform.Rotation = Turn(transform.Rotation, turntable.Speed * seconds);
        });
    }

    public static void Drag(World world, float pixels)
    {
        world.Query(in Turntables, (ref Transform transform, ref Turntable turntable) =>
        {
            transform.Rotation = Turn(transform.Rotation, turntable.DragSpeed * pixels);
        });
    }

    public override void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        Spin(World, seconds);
    }

    /// <summary>The rotation followed by a turn about the world's up axis, so a tilted model keeps its tilt.</summary>
    private static Quaternion Turn(Quaternion rotation, float degrees)
    {
        float radians = float.DegreesToRadians(degrees);
        Quaternion yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians);
        return Quaternion.Normalize(Quaternion.Concatenate(rotation, yaw));
    }
}
