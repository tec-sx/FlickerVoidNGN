using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

/// <summary>Builds the hierarchy under test over a new world of named entities with transforms.</summary>
internal sealed class TransformHierarchyTestsBuilder : IDisposable
{
    public World World { get; } = World.Create();

    public TransformHierarchy Hierarchy { get; } = new();

    public Entity Add(string name, Vector3 position, string? parent = null)
    {
        Transform transform = new() { Position = position };
        Entity entity = World.Create(new EntityName(name), transform);
        if (parent is not null)
        {
            World.Add(entity, new EntityParent(parent));
        }

        return entity;
    }

    public Entity AddUnnamed(Vector3 position)
    {
        Transform transform = new() { Position = position };
        return World.Create(transform);
    }

    public void Move(Entity entity, Vector3 position, Quaternion? rotation = null, Vector3? scale = null)
    {
        Transform transform = World.Get<Transform>(entity);
        transform.Position = position;
        transform.Rotation = rotation ?? transform.Rotation;
        transform.Scale = scale ?? transform.Scale;
        World.Set(entity, transform);
    }

    public Vector3 PositionOf(Entity entity) => World.Get<Transform>(entity).Position;

    public void Dispose() => World.Destroy(World);
}
