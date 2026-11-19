using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Advances every <see cref="MeshAnimator"/> by the frame's time at its speed.</summary>
public sealed class MeshAnimationSystem : ISystem<GameTime>
{
    private static readonly QueryDescription Animators = new QueryDescription().WithAll<MeshAnimator>();

    private readonly World _world;

    public MeshAnimationSystem(World world)
    {
        _world = world;
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        float seconds = (float)t.Elapsed.TotalSeconds;
        _world.Query(in Animators, (ref MeshAnimator animator) => animator.Time += seconds * animator.Speed);
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}

