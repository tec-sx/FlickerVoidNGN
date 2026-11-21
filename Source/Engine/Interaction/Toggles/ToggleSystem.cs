using Arch.Core;

using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Interaction.Toggles;

/// <summary>Opens and closes a toggle when an interaction does its action, unless a locked lock on it blocks it.</summary>
public sealed class ToggleSystem
{
    private readonly World _world;
    private readonly EventBus _events;

    public ToggleSystem(World world, EventBus events)
    {
        _world = world;
        _events = events;
        events.Handle((in InteractionActionEvent action) => Respond(action));
    }

    private void Respond(in InteractionActionEvent action)
    {
        Entity target = action.Target;
        if (!_world.IsAlive(target) || !_world.Has<Toggle>(target))
        {
            return;
        }

        ref Toggle toggle = ref _world.Get<Toggle>(target);
        if (!toggle.Action.IsValid || toggle.Action != action.Action)
        {
            return;
        }

        if (_world.TryGet(target, out Lockable targetLock) && targetLock.Locked)
        {
            _events.Send(new ToggleBlockedEvent(target, action.Interactor, !toggle.Open));
            return;
        }

        toggle.Open = !toggle.Open;
        _events.Send(new ToggledEvent(target, toggle.Open, action.Interactor));
    }
}
