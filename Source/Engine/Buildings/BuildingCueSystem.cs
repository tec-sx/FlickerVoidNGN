using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Switches the camera to a building's shots as activators walk into and out of its trigger areas: on entering, on leaving or both,
/// with a cut, a blend or a fade; a trigger on both goes back on leaving to the shot live when it fired, unless it hands over to the
/// shot of the room walked into. Without a camera director it does nothing.
/// </summary>
public sealed class BuildingCueSystem
{
    private readonly World _world;
    private readonly IServiceRegistry _services;
    private readonly CreationParts _parts;
    private readonly Dictionary<Entity, Entity> _entered = [];

    public BuildingCueSystem(World world, EventBus events, IServiceRegistry services, CreationParts parts)
    {
        _world = world;
        _services = services;
        _parts = parts;
        events.Handle((in AreaEnteredEvent area) => Fire(area.Area, area.Activator, true));
        events.Handle((in AreaExitedEvent area) => Fire(area.Area, area.Activator, false));
    }

    /// <summary>How a trigger's change looks over its time.</summary>
    public static ShotTransition GetTransition(TriggerChange change, float time)
    {
        float seconds = Math.Max(time, 0);
        switch (change)
        {
            case TriggerChange.Blend:
                return new ShotTransition { Kind = TransitionKind.Blend, Duration = seconds };
            case TriggerChange.Fade:
                return new ShotTransition { Kind = TransitionKind.Fade, Duration = seconds / 2, InDuration = seconds / 2 };
            default:
                return ShotTransition.Cut;
        }
    }

    private void Fire(Entity area, Entity activator, bool entering)
    {
        if (!_world.IsAlive(area) || !_world.TryGet(area, out BuildingCue cue) || !_world.TryGet(area, out CreationPart part))
        {
            return;
        }

        if (_services.GetService<CameraDirector>() is not { } director)
        {
            return;
        }

        ShotTransition transition = GetTransition(cue.Change, cue.Time);
        if (!entering && cue.When == TriggerMoment.Both)
        {
            GoBack(area, activator, part.Root, director, transition);
            return;
        }

        bool fires = cue.When == TriggerMoment.Both || (entering && cue.When == TriggerMoment.Enter) || (!entering && cue.When == TriggerMoment.Leave);
        Entity shot = _parts.Find(part.Root, cue.Shot);
        if (!fires || shot == Entity.Null || director.Live == shot)
        {
            return;
        }

        if (cue.When == TriggerMoment.Both)
        {
            _entered[area] = director.Live;
        }

        director.Switch(shot, transition);
    }

    /// <summary>
    /// On leaving a trigger on both: to the shot of another of the building's triggers the activator now stands in when it hands over
    /// to the room entered, else to the shot live when it fired.
    /// </summary>
    private void GoBack(Entity area, Entity activator, Entity root, CameraDirector director, in ShotTransition transition)
    {
        if (!_entered.Remove(area, out Entity before))
        {
            return;
        }

        BuildingCue cue = _world.Get<BuildingCue>(area);
        if (cue.Return == TriggerReturn.RoomEntered && FindEntered(area, activator, root) is { } entered)
        {
            if (director.Live != entered)
            {
                director.Switch(entered, transition);
            }

            return;
        }

        if (before != Entity.Null && _world.IsAlive(before) && director.Live != before)
        {
            director.Switch(before, transition);
        }
    }

    /// <summary>The shot of another trigger of the building the activator stands in, if any.</summary>
    private Entity? FindEntered(Entity area, Entity activator, Entity root)
    {
        if (_services.GetService<TriggerAreaSystem>() is not { } triggers)
        {
            return null;
        }

        foreach (Entity other in _parts.FindAll(root))
        {
            if (other == area || !_world.TryGet(other, out BuildingCue cue) || !triggers.IsInside(other, activator))
            {
                continue;
            }

            Entity shot = _parts.Find(root, cue.Shot);
            if (shot != Entity.Null)
            {
                return shot;
            }
        }

        return null;
    }
}
