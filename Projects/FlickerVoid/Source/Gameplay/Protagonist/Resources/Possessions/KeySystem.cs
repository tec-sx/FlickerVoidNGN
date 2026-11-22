using Arch.Core;

using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Possessions;

using World = Arch.Core.World;

/// <summary>
/// Opens a lock that blocks a toggle with a key whoever was blocked carries, an item with the lock's key tag, and the toggle then goes
/// ahead; a lock that consumes its key takes one unit of it.
/// </summary>
public sealed class KeySystem
{
    private readonly World _world;
    private readonly EventBus _events;
    private readonly InventorySystem _inventories;

    public KeySystem(World world, EventBus events, InventorySystem inventories)
    {
        _world = world;
        _events = events;
        _inventories = inventories;
        events.Handle((in ToggleBlockedEvent blocked) => TryUnlock(blocked));
    }

    private void TryUnlock(in ToggleBlockedEvent blocked)
    {
        Entity target = blocked.Target;
        if (!_world.IsAlive(target) || !_world.Has<Lockable>(target))
        {
            return;
        }

        ref Lockable targetLock = ref _world.Get<Lockable>(target);
        if (!targetLock.Locked || !targetLock.Key.IsValid)
        {
            return;
        }

        ItemStack? key = _inventories.FindItemWithTag(blocked.Instigator, targetLock.Key);
        if (key is null)
        {
            return;
        }

        if (targetLock.ConsumeKey)
        {
            _inventories.RemoveStack(blocked.Instigator, key.Id, 1);
        }

        DoorLocks.Unlock(_world, _events, target, blocked.Instigator, blocked.Opening);
    }
}
