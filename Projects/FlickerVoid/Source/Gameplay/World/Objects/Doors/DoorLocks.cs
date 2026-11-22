using Arch.Core;

using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Gameplay.World.Objects.Doors;

using World = Arch.Core.World;

/// <summary>Opens a lock that blocked a toggle, as a key or a lockpick does, and lets the toggle go ahead.</summary>
public static class DoorLocks
{
    public static void Unlock(World world, EventBus events, Entity door, Entity instigator, bool opening)
    {
        ref Lockable doorLock = ref world.Get<Lockable>(door);
        doorLock.Locked = false;
        events.Send(new LockChangedEvent(door, false, instigator));
        if (!world.Has<Toggle>(door))
        {
            return;
        }

        ref Toggle toggle = ref world.Get<Toggle>(door);
        toggle.Open = opening;
        events.Send(new ToggledEvent(door, toggle.Open, instigator));
    }
}
