using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Spawns the parts of creations under the entities placed in a level: one entity per part, marked with its id, nested under the
/// placed entity so it follows it, and started and stopped with it. A patch brings the parts in line with a newer description by id,
/// so the parts that stay are the same entities and a selection survives an edit of the creation's file. Parts are not scene
/// entities, so a save of the level writes none of them.
/// </summary>
public sealed class CreationParts
{
    private readonly World _world;
    private readonly EntityLifecycle _lifecycle;
    private readonly Dictionary<Entity, List<Spawned>> _spawned = [];
    private readonly HashSet<string> _ids = [];

    public CreationParts(World world, EntityLifecycle lifecycle)
    {
        _world = world;
        _lifecycle = lifecycle;
        ArrayRegistry.Add<CreationPart>();
        ArrayRegistry.Add<PartPlace>();
    }

    /// <summary>
    /// Spawns parts under every entity with a <typeparamref name="T"/> component as it attaches, the ones <paramref name="describe"/>
    /// gives for the component, and destroys them as it detaches.
    /// </summary>
    public void AddKind<T>(Func<T, IReadOnlyList<PartData>> describe)
        where T : struct
    {
        _lifecycle.OnAttach((Entity root, ref T creation) =>
        {
            IReadOnlyList<PartData> parts = describe(creation);
            Patch(root, parts);
        });
        _lifecycle.OnDetach((Entity root, ref T creation) => Remove(root));
    }

    /// <summary>The part of the root with the id, or <see cref="Entity.Null"/>.</summary>
    public Entity Find(Entity root, string id)
    {
        if (!_spawned.TryGetValue(root, out List<Spawned>? parts))
        {
            return Entity.Null;
        }

        foreach (Spawned part in parts)
        {
            if (part.Data.Id == id)
            {
                return part.Entity;
            }
        }

        return Entity.Null;
    }

    /// <summary>The root's parts, in the order its last patch gave them.</summary>
    public IReadOnlyList<Entity> FindAll(Entity root)
    {
        if (!_spawned.TryGetValue(root, out List<Spawned>? parts))
        {
            return [];
        }

        List<Entity> entities = new(parts.Count);
        foreach (Spawned part in parts)
        {
            entities.Add(part.Entity);
        }

        return entities;
    }

    /// <summary>
    /// Brings the root's parts in line with <paramref name="parts"/>: spawns the ids it lacks, destroys the ids that are gone, and on
    /// the parts it keeps sets only what changed since the last patch: the place, and each component between its stop and start
    /// hooks. Spawning is a patch from no parts. Refuses parts that share an id before anything changes.
    /// </summary>
    public void Patch(Entity root, IReadOnlyList<PartData> parts)
    {
        CheckIds(parts);
        if (!_spawned.TryGetValue(root, out List<Spawned>? previous))
        {
            previous = [];
        }

        List<Spawned> current = new(parts.Count);
        List<Entity> started = [];
        foreach (PartData part in parts)
        {
            int match = previous.FindIndex(spawned => spawned.Data.Id == part.Id);
            if (match >= 0 && _world.IsAlive(previous[match].Entity))
            {
                Spawned kept = previous[match];
                Update(root, kept.Entity, kept.Data, part);
                current.Add(kept with { Data = part });
                previous.RemoveAt(match);
            }
            else
            {
                Entity entity = Create(root, part);
                current.Add(new Spawned(part, entity));
                started.Add(entity);
            }
        }

        Destroy(previous);
        _spawned[root] = current;
        foreach (Entity entity in started)
        {
            _lifecycle.Start(_world, entity);
        }
    }

    /// <summary>Destroys the root's parts, last first; does nothing for a root without parts.</summary>
    public void Remove(Entity root)
    {
        if (_spawned.Remove(root, out List<Spawned>? parts))
        {
            Destroy(parts);
        }
    }

    private void CheckIds(IReadOnlyList<PartData> parts)
    {
        _ids.Clear();
        foreach (PartData part in parts)
        {
            if (!_ids.Add(part.Id))
            {
                throw new ArgumentException($"A creation's parts need ids of their own, but two parts have the id '{part.Id}'.", nameof(parts));
            }
        }
    }

    private Entity Create(Entity root, PartData part)
    {
        CreationPart mark = new(root, part.Id);
        PartPlace place = new(part.Place);
        Entity entity = _world.Create(mark, place);
        TransformHierarchy.Attach(_world, entity, root, part.Place);
        foreach (PartComponent component in part.Components)
        {
            component.Set(_world, entity);
        }

        return entity;
    }

    private void Update(Entity root, Entity entity, PartData previous, PartData current)
    {
        if (!Same(previous.Place, current.Place))
        {
            PartPlace place = new(current.Place);
            _world.Set(entity, place);
            TransformHierarchy.Attach(_world, entity, root, current.Place);
        }

        foreach (PartComponent component in current.Components)
        {
            PartComponent? old = previous.Find(component.Type);
            if (old is not null && old.Matches(component))
            {
                continue;
            }

            _lifecycle.StopComponent(_world, entity, component.Type);
            component.Set(_world, entity);
            _lifecycle.StartComponent(_world, entity, component.Type);
        }

        foreach (PartComponent old in previous.Components)
        {
            if (current.Find(old.Type) is null)
            {
                _lifecycle.StopComponent(_world, entity, old.Type);
                old.Remove(_world, entity);
            }
        }
    }

    private void Destroy(List<Spawned> parts)
    {
        for (int index = parts.Count - 1; index >= 0; index--)
        {
            Entity entity = parts[index].Entity;
            if (_world.IsAlive(entity))
            {
                _lifecycle.Destroy(_world, entity);
            }
        }
    }

    private static bool Same(in Transform first, in Transform second)
    {
        return first.Position == second.Position && first.Rotation == second.Rotation && first.Scale == second.Scale;
    }

    /// <summary>A part as the last patch described it, and the entity spawned for it.</summary>
    private readonly record struct Spawned(PartData Data, Entity Entity);
}
