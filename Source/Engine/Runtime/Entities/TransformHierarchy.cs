using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>
/// Moves nested entities (<see cref="EntityParent"/> by name, <see cref="NestedUnder"/> by entity) with their parents, parents first: each child keeps where it stands in its
/// parent's space, a child moved on its own keeps its new place there, and transforms stay in world space for every other system.
/// A missing parent or a loop of parents leaves the child where it is. A game service, which <c>Game</c> updates after each fixed
/// step and before each frame; the editor updates it after each edit. A child keeps the parent it found while that parent lives and
/// keeps its name, so names are looked up only for children that have none.
/// </summary>
public sealed class TransformHierarchy
{
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName>();
    private static readonly QueryDescription Children = new QueryDescription().WithAll<Transform>().WithAny<EntityParent, NestedUnder>();
    private static readonly QueryDescription Detached = new QueryDescription().WithAll<Attachment>().WithNone<EntityParent, NestedUnder>();

    private readonly Dictionary<string, Entity> _byName = [];
    private readonly List<Entity> _children = [];
    private readonly List<Entity> _detached = [];
    private readonly HashSet<Entity> _done = [];
    private readonly HashSet<Entity> _visiting = [];
    private bool _named;

    public TransformHierarchy()
    {
        ArrayRegistry.Add<EntityParent>();
        ArrayRegistry.Add<Attachment>();
        ArrayRegistry.Add<NestedUnder>();
    }

    public void Update(World world)
    {
        Detach(world);
        _named = false;
        _children.Clear();
        world.Query(in Children, (Entity entity) => _children.Add(entity));
        _done.Clear();
        foreach (Entity child in _children)
        {
            Follow(world, child);
        }
    }

    /// <summary>
    /// Nests the child under the parent entity, which wins over a parent named by <see cref="EntityParent"/>, and puts it at
    /// <paramref name="place"/> in the parent's space now, so it stands there even if the parent moves before the next update; a
    /// parent without a transform stands at the origin.
    /// </summary>
    public static void Attach(World world, Entity child, Entity parent, in Transform place)
    {
        if (!world.TryGet(parent, out Transform parentWorld))
        {
            parentWorld = new Transform();
        }

        Transform placed = ToWorld(parentWorld, place);
        NestedUnder nested = new(parent);
        Attachment attachment = new() { Parent = parent, Local = place, Applied = placed };
        Set(world, child, placed);
        Set(world, child, nested);
        Set(world, child, attachment);
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
        Entity parent = FindParent(world, child);
        bool found = parent != Entity.Null && parent != child && world.Has<Transform>(parent);
        if (found && (world.Has<EntityParent>(parent) || world.Has<NestedUnder>(parent)))
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

    /// <summary>
    /// The entity the child is nested under while it lives; else the parent the child found before while it lives under the name it
    /// has, else the first entity with the name; else <see cref="Entity.Null"/>.
    /// </summary>
    private Entity FindParent(World world, Entity child)
    {
        if (world.TryGet(child, out NestedUnder nested))
        {
            if (world.IsAlive(nested.Parent))
            {
                return nested.Parent;
            }

            return Entity.Null;
        }

        EntityParent nameGiven = world.Get<EntityParent>(child);
        string name = nameGiven.Name;
        if (world.TryGet(child, out Attachment attachment) && world.IsAlive(attachment.Parent) && world.TryGet(attachment.Parent, out EntityName parentName) && parentName.Value == name)
        {
            return attachment.Parent;
        }

        if (!_named)
        {
            _byName.Clear();
            world.Query(in Named, (Entity entity, ref EntityName named) => _byName.TryAdd(named.Value, entity));
            _named = true;
        }

        if (_byName.TryGetValue(name, out Entity parent))
        {
            return parent;
        }

        return Entity.Null;
    }

    private static void Set<T>(World world, Entity entity, in T component)
    {
        if (world.Has<T>(entity))
        {
            world.Set(entity, component);
        }
        else
        {
            world.Add(entity, component);
        }
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
