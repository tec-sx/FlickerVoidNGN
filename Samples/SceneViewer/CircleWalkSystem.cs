using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>Walks every animated entity round the origin at its distance from it, facing the way it goes (its model's +Z).</summary>
public sealed class CircleWalkSystem : ISystem<GameTime>
{
    private static readonly QueryDescription Walkers = new QueryDescription().WithAll<Transform, MeshAnimator>();

    private readonly World _world;

    /// <summary>Walks at <paramref name="speed"/> world units per second.</summary>
    public CircleWalkSystem(World world, float speed)
    {
        _world = world;
        Speed = speed;
    }

    /// <summary>How fast they walk, in world units per second.</summary>
    public float Speed { get; set; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        float speed = Speed;
        _world.Query(in Walkers, (ref Transform transform) =>
        {
            Vector3 position = transform.Position;
            float radius = MathF.Sqrt((position.X * position.X) + (position.Z * position.Z));
            if (radius < 1e-3f)
            {
                return;
            }

            float angle = MathF.Atan2(position.Z, position.X) - (speed * seconds / radius);
            Vector3 heading = new(MathF.Sin(angle), 0, -MathF.Cos(angle));
            transform.Position = new Vector3(MathF.Cos(angle) * radius, position.Y, MathF.Sin(angle) * radius);
            float yaw = MathF.Atan2(heading.X, heading.Z);
            transform.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
        });
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}
