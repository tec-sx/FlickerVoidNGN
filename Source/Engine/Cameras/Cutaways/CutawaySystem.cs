using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras.Cutaways;

/// <summary>
/// Opens the building the camera's target stands in: the smallest indoor volume around the first <see cref="CameraTarget"/>
/// becomes the <see cref="Cutaway"/>, and everything nested under the same top entity (the building) that stands at or above
/// that volume's ceiling (the floors above, the roof) only casts shadows. Outside every volume, nothing is cut. Storeys are
/// indoor volumes nested in their building, and the pieces of each storey stand on their entity, as greyboxes do.
/// </summary>
public sealed class CutawaySystem : BaseSystem<World, GameTime>
{
    /// <summary>How far a target's feet or a piece's base may stray past a volume's box and still count as on it.</summary>
    private const float Margin = 0.05f;

    private static readonly QueryDescription Targets = new QueryDescription().WithAll<CameraTarget, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IndoorVolume, Transform>();
    private static readonly QueryDescription Named = new QueryDescription().WithAll<EntityName>();
    private static readonly QueryDescription Placed = new QueryDescription().WithAll<Transform>();

    /// <summary>How far past their boxes indoor volumes reach in the scene shader, so walls standing on them count as inside.</summary>
    private const float IndoorMargin = 0.05f;

    private readonly HashSet<Entity> _hidden = [];
    private readonly HashSet<Entity> _hiding = [];
    private Entity? _cutaway;

    public CutawaySystem(World world)
        : base(world)
    {
    }

    /// <summary>The indoor volume the camera looks into, if any.</summary>
    public Entity? Cutaway => _cutaway;

    /// <summary>
    /// Whether the camera can't see the entity at <paramref name="point"/>, looking along <paramref name="away"/>: it only casts
    /// shadows, or the point stands in front of the cutaway, as the scene shader cuts it. Clicks pass through what is unseen.
    /// </summary>
    public bool IsUnseen(Entity entity, Vector3 point, Vector3 away)
    {
        if (World.IsAlive(entity) && World.Has<ShadowOnly>(entity))
        {
            return true;
        }

        if (_cutaway is not { } opened || !World.IsAlive(opened))
        {
            return false;
        }

        IndoorVolume volume = World.Get<IndoorVolume>(opened);
        Transform transform = World.Get<Transform>(opened);
        (Vector3 min, Vector3 max) = volume.GetBounds(transform.Position, transform.Rotation);
        return IsInFront(min - new Vector3(IndoorMargin), max + new Vector3(IndoorMargin), point, away);
    }

    /// <summary>Whether a point outside the box has the box behind it along <paramref name="away"/>.</summary>
    private static bool IsInFront(Vector3 min, Vector3 max, Vector3 point, Vector3 away)
    {
        bool inside = point.X >= min.X && point.Y >= min.Y && point.Z >= min.Z && point.X <= max.X && point.Y <= max.Y && point.Z <= max.Z;
        if (inside)
        {
            return false;
        }

        float enter = 0;
        float leave = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            float direction = away[axis];
            if (MathF.Abs(direction) < 1e-6f)
            {
                if (point[axis] < min[axis] || point[axis] > max[axis])
                {
                    return false;
                }

                continue;
            }

            float toMin = (min[axis] - point[axis]) / direction;
            float toMax = (max[axis] - point[axis]) / direction;
            enter = MathF.Max(enter, MathF.Min(toMin, toMax));
            leave = MathF.Min(leave, MathF.Max(toMin, toMax));
        }

        return enter <= leave;
    }

    public override void Update(in GameTime t)
    {
        Entity? volume = FindVolume();
        if (volume is { } alive && !World.IsAlive(alive))
        {
            volume = null;
        }

        if (volume == _cutaway)
        {
            return;
        }

        if (_cutaway is { } old && World.IsAlive(old) && World.Has<Cutaway>(old))
        {
            World.Remove<Cutaway>(old);
        }

        _cutaway = volume;
        _hiding.Clear();
        if (volume is { } opened)
        {
            World.Add(opened, new Cutaway());
            CollectAbove(opened);
        }

        foreach (Entity entity in _hidden)
        {
            if (!_hiding.Contains(entity) && World.IsAlive(entity) && World.Has<ShadowOnly>(entity))
            {
                World.Remove<ShadowOnly>(entity);
            }
        }

        foreach (Entity entity in _hiding)
        {
            if (!World.Has<ShadowOnly>(entity))
            {
                World.Add(entity, new ShadowOnly());
            }
        }

        _hidden.Clear();
        _hidden.UnionWith(_hiding);
    }

    /// <summary>The smallest indoor volume holding the first camera target, or none.</summary>
    private Entity? FindVolume()
    {
        Vector3? target = null;
        World.Query(in Targets, (ref Transform transform) => target ??= transform.Position);
        if (target is not { } position)
        {
            return null;
        }

        Entity? found = null;
        float smallest = float.PositiveInfinity;
        World.Query(in Volumes, (Entity entity, ref IndoorVolume volume, ref Transform transform) =>
        {
            (Vector3 min, Vector3 max) = volume.GetBounds(transform.Position, transform.Rotation);
            bool inside = Contains(min, max, position);
            Vector3 size = max - min;
            float space = size.X * size.Y * size.Z;
            if (inside && space < smallest)
            {
                found = entity;
                smallest = space;
            }
        });
        return found;
    }

    private static bool Contains(Vector3 min, Vector3 max, Vector3 position)
    {
        Vector3 low = min - new Vector3(Margin);
        Vector3 high = max + new Vector3(Margin);
        bool aboveLow = position.X >= low.X && position.Y >= low.Y && position.Z >= low.Z;
        bool belowHigh = position.X <= high.X && position.Y <= high.Y && position.Z <= high.Z;
        return aboveLow && belowHigh;
    }

    /// <summary>Collects the entities of the volume's building that stand at or above its ceiling.</summary>
    private void CollectAbove(Entity volume)
    {
        Dictionary<string, Entity> byName = [];
        World.Query(in Named, (Entity entity, ref EntityName name) => byName[name.Value] = entity);
        Entity building = FindTop(volume, byName);
        IndoorVolume indoor = World.Get<IndoorVolume>(volume);
        Transform placement = World.Get<Transform>(volume);
        (_, Vector3 max) = indoor.GetBounds(placement.Position, placement.Rotation);
        float ceiling = max.Y - Margin;
        World.Query(in Placed, (Entity entity, ref Transform transform) =>
        {
            if (entity != building && transform.Position.Y >= ceiling && IsUnder(entity, building, byName))
            {
                _hiding.Add(entity);
            }
        });
    }

    private Entity FindTop(Entity entity, Dictionary<string, Entity> byName)
    {
        Entity top = entity;
        for (int depth = 0; depth < 64 && World.TryGet(top, out EntityParent parent) && byName.TryGetValue(parent.Name, out Entity above); depth++)
        {
            top = above;
        }

        return top;
    }

    private bool IsUnder(Entity entity, Entity ancestor, Dictionary<string, Entity> byName)
    {
        Entity current = entity;
        for (int depth = 0; depth < 64 && World.TryGet(current, out EntityParent parent) && byName.TryGetValue(parent.Name, out Entity above); depth++)
        {
            if (above == ancestor)
            {
                return true;
            }

            current = above;
        }

        return false;
    }
}
