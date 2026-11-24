using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>
/// Hears activators enter and leave trigger areas with a <see cref="CameraCue"/> and asks the director for what the cue says;
/// a cue on both goes back on leaving from what it did on entering, and saves keep which saveable cues that fire once already fired.
/// </summary>
public sealed class CameraCueSystem
{
    private readonly Game _game;
    private readonly CameraDirector _director;
    private readonly HashSet<Entity> _fired = [];
    private readonly Dictionary<Entity, Entity> _entered = [];

    public CameraCueSystem(Game game, CameraDirector director)
    {
        _game = game;
        _director = director;
        game.Events.Handle((in AreaEnteredEvent area) => Fire(area.Area, CueMoment.Enter));
        game.Events.Handle((in AreaExitedEvent area) => Fire(area.Area, CueMoment.Exit));
        game.Events.Handle((in AreaExitedEvent area) => Leave(area.Area));
        game.SaveStates.Add<SavedCue>("cameraCue", Save, Restore);
    }

    private void Fire(Entity area, CueMoment moment)
    {
        World world = _game.World;
        if (!world.IsAlive(area) || !world.TryGet(area, out CameraCue cue) || _fired.Contains(area))
        {
            return;
        }

        bool both = cue.On == CueMoment.Both && moment == CueMoment.Enter;
        if (cue.On != moment && !both)
        {
            return;
        }

        ConditionContext context = new(_game.Facts) { Services = _game.Services };
        foreach (FactCondition condition in cue.When)
        {
            if (!condition.Check(context))
            {
                return;
            }
        }

        if (cue.Once)
        {
            _fired.Add(area);
        }

        if (both)
        {
            _entered[area] = _director.Live;
        }

        Entity shot = _director.FindShot(cue.Shot);
        bool named = !string.IsNullOrEmpty(cue.Shot);
        if (cue.Mode == ShotSwitchMode.Return || shot != Entity.Null)
        {
            _director.Request(cue.Mode, shot, cue.Transition);
        }

        if (cue.Play && named)
        {
            _director.Play(shot, cue.ToKey, cue.Layers);
        }

        if (cue.ShakeStrength > 0)
        {
            _director.Shake(cue.ShakeStrength, cue.ShakeDuration);
        }
    }

    private void Leave(Entity area)
    {
        if (!_entered.Remove(area, out Entity before) || !_game.World.IsAlive(area) || !_game.World.TryGet(area, out CameraCue cue))
        {
            return;
        }

        if (cue.Mode == ShotSwitchMode.Push)
        {
            _director.Return(cue.ExitTransition);
            return;
        }

        Entity shot = _director.FindShot(cue.ExitShot);
        if (shot == Entity.Null && string.IsNullOrEmpty(cue.ExitShot))
        {
            shot = before;
        }

        if (shot != Entity.Null && _game.World.IsAlive(shot))
        {
            _director.Switch(shot, cue.ExitTransition);
        }
    }

    private SavedCue? Save(Entity entity)
    {
        if (!_game.World.Has<CameraCue>(entity))
        {
            return null;
        }

        return new SavedCue { Fired = _fired.Contains(entity) };
    }

    private void Restore(Entity entity, SavedCue saved)
    {
        if (saved.Fired)
        {
            _fired.Add(entity);
        }
        else
        {
            _fired.Remove(entity);
        }
    }
}
