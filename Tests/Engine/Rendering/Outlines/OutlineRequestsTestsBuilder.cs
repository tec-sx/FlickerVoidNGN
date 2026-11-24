using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Outlines;

namespace FlickerVoid.Engine.Rendering.Tests.Outlines;

/// <summary>Builds a world with an entity to request outlines on, with or without an outline of its own.</summary>
internal sealed class OutlineRequestsTestsBuilder : IDisposable
{
    private readonly World _world = World.Create();

    public World World => _world;

    public Entity Spawn(Vector3? authored = null)
    {
        Entity entity = _world.Create();
        if (authored is { } color)
        {
            World.Add(entity, new Outline { Color = color });
        }

        return entity;
    }

    /// <summary>The entity's outline colour, or null without one.</summary>
    public Vector3? ColorOf(Entity entity)
    {
        if (World.TryGet(entity, out Outline outline))
        {
            return outline.Color;
        }

        return null;
    }

    public void Dispose() => World.Destroy(World);
}
