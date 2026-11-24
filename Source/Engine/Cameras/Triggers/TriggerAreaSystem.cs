using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>
/// Checks each fixed step which activators stand in which trigger areas, sending <see cref="AreaEnteredEvent"/> and
/// <see cref="AreaExitedEvent"/> as that changes (no exit from an area that is gone); a service, so others can ask who is inside.
/// Whoever a load puts back in an area is inside without entering, as the save holds what entering set off.
/// </summary>
public sealed class TriggerAreaSystem : BaseSystem<World, GameTime>
{
    private static readonly QueryDescription Areas = new QueryDescription().WithAll<TriggerArea, Transform>();
    private static readonly QueryDescription Activators = new QueryDescription().WithAll<TriggerActivator, Transform>();

    private readonly EventBus _events;
    private readonly IServiceRegistry _services;
    private readonly HashSet<(Entity Area, Entity Activator)> _inside = [];
    private readonly HashSet<(Entity Area, Entity Activator)> _now = [];
    private readonly List<Entity> _areas = [];
    private readonly List<Entity> _activators = [];
    private SaveSystem? _saves;

    public TriggerAreaSystem(World world, EventBus events, IServiceRegistry services)
        : base(world)
    {
        _events = events;
        _services = services;
    }

    public bool IsInside(Entity area, Entity activator) => _inside.Contains((area, activator));

    public override void Initialize()
    {
        _saves = _services.GetService<SaveSystem>();
        if (_saves is not null)
        {
            _saves.Loaded += HandleLoaded;
        }
    }

    public override void Update(in GameTime t)
    {
        FindInside();
        foreach ((Entity area, Entity activator) in _inside)
        {
            if (!_now.Contains((area, activator)) && World.IsAlive(area))
            {
                _events.Send(new AreaExitedEvent(area, activator));
            }
        }

        foreach ((Entity area, Entity activator) in _now)
        {
            if (!_inside.Contains((area, activator)))
            {
                _events.Send(new AreaEnteredEvent(area, activator));
            }
        }

        _inside.Clear();
        _inside.UnionWith(_now);
    }

    public override void Dispose()
    {
        if (_saves is not null)
        {
            _saves.Loaded -= HandleLoaded;
        }

        base.Dispose();
    }

    /// <summary>Finds which activators stand in which areas now.</summary>
    private void FindInside()
    {
        _areas.Clear();
        _activators.Clear();
        World.Query(in Areas, (Entity entity) => _areas.Add(entity));
        World.Query(in Activators, (Entity entity) => _activators.Add(entity));

        _now.Clear();
        foreach (Entity area in _areas)
        {
            TriggerArea trigger = World.Get<TriggerArea>(area);
            Transform transform = World.Get<Transform>(area);
            Matrix4x4 areaWorld = transform.ToMatrix();
            Matrix4x4.Invert(areaWorld, out Matrix4x4 toArea);
            foreach (Entity activator in _activators)
            {
                Vector3 position = World.Get<Transform>(activator).Position;
                Vector3 local = Vector3.Transform(position, toArea);
                if (Contains(trigger, local) && HasTags(trigger, activator))
                {
                    _now.Add((area, activator));
                }
            }
        }
    }

    private void HandleLoaded(SaveGame save)
    {
        FindInside();
        _inside.Clear();
        _inside.UnionWith(_now);
    }

    private static bool Contains(in TriggerArea trigger, Vector3 local)
    {
        Vector3 fromCentre = Vector3.Abs(local - trigger.Offset);
        Vector3 half = trigger.Size * 0.5f;
        return fromCentre.X <= half.X && fromCentre.Y <= half.Y && fromCentre.Z <= half.Z;
    }

    private bool HasTags(in TriggerArea trigger, Entity activator)
    {
        if (trigger.RequiredTags.Count == 0)
        {
            return true;
        }

        if (!World.TryGet(activator, out OwnedTags owned))
        {
            return false;
        }

        foreach (Tag required in trigger.RequiredTags)
        {
            if (!owned.HasMatchingTag(required))
            {
                return false;
            }
        }

        return true;
    }
}
