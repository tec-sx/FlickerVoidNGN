using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>
/// Lets pickups be taken into inventories, as much as fits, or used where they lie; an emptied pickup is destroyed once its event is
/// handled, unless it is kept.
/// </summary>
public sealed partial class PickupSystem
{
    private readonly World _world;
    private readonly EventBus _events;
    private readonly EntityLifecycle _lifecycle;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly InventorySystem _inventories;
    private readonly ILogger<PickupSystem> _logger;

    public PickupSystem(Game game, InventorySystem inventories)
    {
        _world = game.World;
        _events = game.Events;
        _lifecycle = game.Lifecycle;
        _facts = game.Facts;
        _services = game.Services;
        _inventories = inventories;
        _logger = game.LoggerFactory.CreateLogger<PickupSystem>();
        ArrayRegistry.Add<ActivePickup>();
        game.Lifecycle.OnActivate((Entity entity, ref Pickup pickup) => Activate(entity, pickup));
        game.Lifecycle.OnDeactivate((Entity entity, ref Pickup _) => Deactivate(entity));
        game.Events.Handle((in PickedUpEvent picked) => RemoveIfEmpty(picked.Pickup));
        game.Events.Handle((in UsedInPlaceEvent used) => RemoveIfEmpty(used.Pickup));
        game.SaveStates.Add<SavedPickup>("pickup", Save, Restore);
    }

    /// <summary>Something is left and the picker has an inventory.</summary>
    public bool CanPickUp(Entity picker, Entity pickup)
    {
        return TryGetPickup(pickup, out ActivePickup active) && !active.IsEmpty && _inventories.HasInventory(picker);
    }

    /// <summary>Moves as much of the pickup as fits into the picker's inventory, a partly used unit first; false when nothing was taken.</summary>
    public bool TryPickUp(Entity picker, Entity pickup)
    {
        if (!CanPickUp(picker, pickup))
        {
            return false;
        }

        ItemStack stack = _world.Get<ActivePickup>(pickup).Stack;
        int taken = _inventories.Receive(picker, stack);
        if (taken <= 0)
        {
            return false;
        }

        // A partial pickup leaves the rest; the partly used unit went first, so the units left are full.
        stack.Quantity = Math.Max(0, stack.Quantity - taken);
        if (stack.Quantity > 0)
        {
            stack.RemoveData<ChargesLeft>();
        }

        _events.Send(new PickedUpEvent(pickup, picker, taken));
        return true;
    }

    /// <summary>Something is left, its item is usable and its use conditions hold with the user as instigator and target.</summary>
    public bool CanUseInPlace(Entity user, Entity pickup)
    {
        if (!TryGetPickup(pickup, out ActivePickup active) || active.IsEmpty || !_world.IsAlive(user))
        {
            return false;
        }

        UsableFragment? usable = active.Stack.Item.FindFragment<UsableFragment>();
        if (usable is null)
        {
            return false;
        }

        ConditionContext context = CreateContext(user);
        return usable.UseConditions.Evaluate(context);
    }

    /// <summary>Uses the item where it lies, as an inventory uses a stack: its effects apply to the user, then a charge or a unit is spent.</summary>
    public bool UseInPlace(Entity user, Entity pickup)
    {
        if (!CanUseInPlace(user, pickup))
        {
            return false;
        }

        ItemStack stack = _world.Get<ActivePickup>(pickup).Stack;
        UsableFragment usable = stack.Item.FindFragment<UsableFragment>()!;
        ConditionContext context = CreateContext(user);
        usable.Effects.Apply(context);
        stack.SpendUse();
        _events.Send(new UsedInPlaceEvent(pickup, user));
        return true;
    }

    private void Activate(Entity entity, Pickup pickup)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null)
        {
            LogNoDataAssets(pickup.Item);
            return;
        }

        ItemDefinition item;
        try
        {
            item = data.Load<ItemDefinition>(pickup.Item);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogItemFailed(pickup.Item, exception.Message);
            return;
        }

        ItemStack stack = new(item, pickup.Quantity);
        _world.Add(entity, new ActivePickup { Stack = stack, DestroyWhenEmpty = pickup.DestroyWhenEmpty });
    }

    private SavedPickup? Save(Entity entity)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.TryGet(entity, out ActivePickup pickup))
        {
            return null;
        }

        SavedStack? stack = SavedStack.From(pickup.Stack, data);
        if (stack is null)
        {
            return null;
        }

        return new SavedPickup { Stack = stack };
    }

    private void Restore(Entity entity, SavedPickup saved)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.Has<ActivePickup>(entity))
        {
            return;
        }

        ItemStack? stack = saved.Stack.ToStack(data);
        if (stack is not null)
        {
            _world.Get<ActivePickup>(entity).Stack = stack;
        }
    }

    private void Deactivate(Entity entity)
    {
        if (_world.Has<ActivePickup>(entity))
        {
            _world.Remove<ActivePickup>(entity);
        }
    }

    private void RemoveIfEmpty(Entity pickup)
    {
        if (TryGetPickup(pickup, out ActivePickup active) && active.IsEmpty && active.DestroyWhenEmpty)
        {
            _lifecycle.Destroy(_world, pickup);
        }
    }

    private bool TryGetPickup(Entity pickup, out ActivePickup active)
    {
        if (!_world.IsAlive(pickup))
        {
            active = default;
            return false;
        }

        return _world.TryGet(pickup, out active);
    }

    private ConditionContext CreateContext(Entity user)
    {
        WorldEntityView view = new(_world, user);
        return new ConditionContext(_facts) { Instigator = view, Target = view, Services = _services };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The pickup of {Item} has no data assets to load its item from; it stays inactive.")]
    private partial void LogNoDataAssets(string item);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The pickup's item {Item} can't be read, so it stays inactive: {Reason}")]
    private partial void LogItemFailed(string item, string reason);
}
