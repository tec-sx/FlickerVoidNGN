using Arch.Core;

using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Interaction.Locks;

/// <summary>Locks and unlocks a lock when an interaction does its action, before toggles respond, so one action can unlock and open a door.</summary>
public sealed class LockSystem
{
    private readonly World _world;
    private readonly EventBus _events;

    public LockSystem(World world, EventBus events)
    {
        _world = world;
        _events = events;
        events.Handle((in InteractionActionEvent action) => Respond(action));
    }

    private void Respond(in InteractionActionEvent action)
    {
        Entity target = action.Target;
        if (!_world.IsAlive(target) || !_world.Has<Lockable>(target))
        {
            return;
        }

        ref Lockable targetLock = ref _world.Get<Lockable>(target);
        if (!targetLock.Action.IsValid || targetLock.Action != action.Action)
        {
            return;
        }

        targetLock.Locked = !targetLock.Locked;
        _events.Send(new LockChangedEvent(target, targetLock.Locked, action.Interactor));
    }
}
