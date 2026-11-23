using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>
/// Moves nested entities (<see cref="EntityParent"/>) with their parents, parents first: each child keeps where it stands in its
/// parent's space, a child moved on its own keeps its new place there, and transforms stay in world space for every other system.
/// A missing parent or a loop of parents leaves the child where it is. A game service, which <c>Game</c> updates after each fixed
/// step and before each frame; the editor updates it after each edit.
/// </summary>
public sealed class TransformHierarchy
{
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName>();
    private static readonly QueryDescription Children = new QueryDescription().WithAll<EntityParent, Transform>();
    private static readonly QueryDescription Detached = new QueryDescription().WithAll<Attachment>().WithNone<EntityParent>();

    private readonly Dictionary<string, Entity> _byName = [];
    private readonly List<Entity> _children = [];
    private readonly List<Entity> _detached = [];
    private readonly HashSet<Entity> _done = [];
    private readonly HashSet<Entity> _visiting = [];

    public TransformHierarchy()
    {
        ArrayRegistry.Add<EntityParent>();
        ArrayRegistry.Add<Attachment>();
    }

    public void Update(World world)
    {
        Detach(world);
        _byName.Clear();
        world.Query(in Named, (Entity entity, ref EntityName name) =>
        {
            _byName.TryAdd(name.Value, entity);
        });
        _children.Clear();
        world.Query(in Children, (Entity entity) => _children.Add(entity));
        _done.Clear();
        foreach (Entity child in _children)
        {
            Follow(world, child);
        }
    }

    /// <summary>Whether the entity follows a parent it found, so moving the parent moves it.</summary>
    public static bool IsAttached(World world, Entity entity) => world.Has<Attachment>(entity);

    private void Detach(World world)
    {
        _detached.Clear();
        world.Query(in Detached, (Entity entity) => _detached.Add(entity));
        foreach (Entity entity in _detached)
        {
            world.Remove<Attachment>(entity);
        }
    }

    /// <summary>Places the child after its own parent, if that is nested too; false when the child sits in a loop of parents.</summary>
    private bool Follow(World world, Entity child)
    {
        if (_done.Contains(child))
        {
            return true;
        }

        if (!_visiting.Add(child))
        {
            return false;
        }

        bool placed = Place(world, child);
        _visiting.Remove(child);
        _done.Add(child);
        return placed;
    }

    private bool Place(World world, Entity child)
    {
        EntityParent parentName = world.Get<EntityParent>(child);
        bool found = _byName.TryGetValue(parentName.Name, out Entity parent) && parent != child && world.Has<Transform>(parent);
        if (found && world.Has<EntityParent>(parent))
        {
            found = Follow(world, parent);
        }

        if (!found)
        {
            if (world.Has<Attachment>(child))
            {
                world.Remove<Attachment>(child);
            }

            return false;
        }

        Transform parentWorld = world.Get<Transform>(parent);
        Transform current = world.Get<Transform>(child);
        if (!world.TryGet(child, out Attachment attachment) || attachment.Parent != parent || !Same(current, attachment.Applied))
        {
            Attachment attached = new() { Parent = parent, Local = ToLocal(parentWorld, current), Applied = current };
            if (world.Has<Attachment>(child))
            {
                world.Set(child, attached);
            }
            else
            {
                world.Add(child, attached);
            }

            return true;
        }

        Transform placed = ToWorld(parentWorld, attachment.Local);
        attachment.Applied = placed;
        world.Set(child, placed);
        world.Set(child, attachment);
        return true;
    }

    private static bool Same(in Transform first, in Transform second)
    {
        return first.Position == second.Position && first.Rotation == second.Rotation && first.Scale == second.Scale;
    }

    private static Transform ToWorld(in Transform parent, in Transform local)
    {
        Vector3 scaled = local.Position * parent.Scale;
        Vector3 turned = Vector3.Transform(scaled, parent.Rotation);
        Quaternion rotation = Quaternion.Normalize(parent.Rotation * local.Rotation);
        return new Transform { Position = parent.Position + turned, Rotation = rotation, Scale = parent.Scale * local.Scale };
    }

    private static Transform ToLocal(in Transform parent, in Transform world)
    {
        Quaternion inverse = Quaternion.Conjugate(parent.Rotation);
        Vector3 offset = world.Position - parent.Position;
        Vector3 turned = Vector3.Transform(offset, inverse);
        Vector3 scale = SafeScale(parent.Scale);
        Quaternion rotation = Quaternion.Normalize(inverse * world.Rotation);
        return new Transform { Position = turned / scale, Rotation = rotation, Scale = world.Scale / scale };
    }

    /// <summary>The parent's scale with zero axes taken as one, so a flattened parent doesn't divide by zero.</summary>
    private static Vector3 SafeScale(Vector3 scale)
    {
        float x = scale.X == 0 ? 1 : scale.X;
        float y = scale.Y == 0 ? 1 : scale.Y;
        float z = scale.Z == 0 ? 1 : scale.Z;
        return new Vector3(x, y, z);
    }
}
