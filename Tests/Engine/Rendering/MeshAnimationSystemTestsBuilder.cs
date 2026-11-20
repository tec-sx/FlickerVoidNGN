using Arch.Core;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds a mesh animation system over a world of animators, in the order they were added.</summary>
internal sealed class MeshAnimationSystemTestsBuilder : IDisposable
{
    private readonly World _world = World.Create();
    private readonly List<Entity> _entities = [];

    public float[] Times => [.. _entities.Select(entity => _world.Get<MeshAnimator>(entity).Time)];

    public MeshAnimationSystemTestsBuilder WithAnimator(float time, float speed)
    {
        MeshAnimator animator = new() { Time = time, Speed = speed };
        Entity entity = _world.Create(animator);
        _entities.Add(entity);
        return this;
    }

    public MeshAnimationSystem Build()
    {
        return new MeshAnimationSystem(_world);
    }

    public static GameTime Frame(float seconds)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(seconds);
        return new GameTime(elapsed, elapsed, 1);
    }

    public void Dispose()
    {
        World.Destroy(_world);
    }
}
