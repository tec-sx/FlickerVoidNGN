using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>
/// Holds the items and money of inventories while the game plays. Units of an item without instance data merge up to the item's
/// stack size; each stack claims the item's cells, and key items take no weight or cells. Worn bags add room, and worn stacks are
/// removed and used last and never transferred. Changes are sent as events.
/// </summary>
public sealed partial class InventorySystem
{
    private const float WeightTolerance = 1e-4f;

    private readonly World _world;

    private readonly EventBus _events;

    private readonly IFactDatabase _facts;

    private readonly IServiceRegistry _services;

    private readonly ILogger<InventorySystem> _logger;

    public InventorySystem(Game game, InventorySettings settings)
    {
        _world = game.World;
        _events = game.Events;
        _facts = game.Facts;
        _services = game.Services;
        _logger = game.LoggerFactory.CreateLogger<InventorySystem>();
        Settings = settings;
        ArrayRegistry.Add<ActiveInventory>();
        game.Lifecycle.OnActivate((Entity entity, ref Inventory inventory) => Activate(entity, inventory));
        game.Lifecycle.OnDeactivate((Entity entity, ref Inventory _) => Deactivate(entity));
        game.SaveStates.Add<SavedInventory>("inventory", Save, Restore);
    }

    public InventorySettings Settings { get; }

    /// <summary>Whether the entity has an inventory while the game plays.</summary>
    public bool HasInventory(Entity owner) => TryGetInventory(owner, out _);

    /// <summary>The owner's stacks in order; empty when it has no inventory.</summary>
    public IReadOnlyList<ItemStack> GetStacks(Entity owner)
    {
        List<ItemStack>? stacks = FindStacks(owner);
        if (stacks is null)
        {
            return [];
        }

        return stacks;
    }

    public ItemStack? FindStack(Entity owner, Guid id)
    {
        List<ItemStack>? stacks = FindStacks(owner);
        if (stacks is null || id == Guid.Empty)
        {
            return null;
        }

        return stacks.Find(stack => stack.Id == id);
    }

    /// <summary>Total units across all stacks of the item.</summary>
    public int GetQuantity(Entity owner, ItemDefinition item)
    {
        int total = 0;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (stack.Item == item)
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    public bool HasItem(Entity owner, ItemDefinition item, int quantity = 1) => GetQuantity(owner, item) >= quantity;

    /// <summary>Stacks whose item's category is <paramref name="category"/> or lies under it.</summary>
    public IReadOnlyList<ItemStack> GetItemsInCategory(Entity owner, Tag category)
    {
        IEnumerable<ItemStack> stacks = GetStacks(owner);
        return stacks.Where(stack => stack.Item.Category.MatchesTag(category)).ToList();
    }

    /// <summary>Stacks of everything but key items: the storage list.</summary>
    public IReadOnlyList<ItemStack> GetStorageItems(Entity owner)
    {
        IEnumerable<ItemStack> stacks = GetStacks(owner);
        return stacks.Where(stack => !Settings.IsKeyItem(stack.Item)).ToList();
    }

    /// <summary>Stacks of key items: the Keys and Quest list.</summary>
    public IReadOnlyList<ItemStack> GetKeyItems(Entity owner)
    {
        IEnumerable<ItemStack> stacks = GetStacks(owner);
        return stacks.Where(stack => Settings.IsKeyItem(stack.Item)).ToList();
    }

    /// <summary>The first stack whose item has <paramref name="tag"/> or a tag under it, such as a key for a lock or a lockpick.</summary>
    public ItemStack? FindItemWithTag(Entity owner, Tag tag)
    {
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (HasTag(stack.Item, tag))
            {
                return stack;
            }
        }

        return null;
    }

    /// <summary>Total units of items that have <paramref name="tag"/> or a tag under it.</summary>
    public int GetQuantityWithTag(Entity owner, Tag tag)
    {
        int total = 0;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (HasTag(stack.Item, tag))
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    private static bool CanMerge(ItemStack stack, ItemDefinition item) => stack.Item == item && stack.IsPlain;

    private static bool HasTag(ItemDefinition item, Tag tag)
    {
        if (!tag.IsValid)
        {
            return false;
        }

        foreach (Tag itemTag in item.Tags)
        {
            if (itemTag.MatchesTag(tag))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds units to <paramref name="stacks"/>, filling plain stacks of the item before starting new ones, and lists the stacks it changed.</summary>
    private static void Merge(List<ItemStack> stacks, ItemDefinition item, int quantity, List<Guid> changed)
    {
        int maxPerStack = item.MaxQuantity;
        int left = quantity;
        foreach (ItemStack stack in stacks)
        {
            if (left <= 0 || !CanMerge(stack, item))
            {
                continue;
            }

            int taken = left;
            if (maxPerStack > 0)
            {
                taken = Math.Min(left, maxPerStack - stack.Quantity);
            }

            if (taken > 0)
            {
                stack.Quantity += taken;
                left -= taken;
                changed.Add(stack.Id);
            }
        }

        while (left > 0)
        {
            int units = left;
            if (maxPerStack > 0)
            {
                units = Math.Min(left, maxPerStack);
            }

            ItemStack added = new(item, units) { Id = Guid.NewGuid() };
            stacks.Add(added);
            left -= units;
            changed.Add(added.Id);
        }
    }

    private void Activate(Entity entity, Inventory inventory)
    {
        List<ItemStack> stacks = [];
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null && inventory.Items.Count > 0)
        {
            LogNoDataAssets();
        }

        List<Guid> changed = [];
        foreach (ItemEntry entry in inventory.Items)
        {
            if (data is null)
            {
                break;
            }

            ItemDefinition item;
            try
            {
                item = data.Load<ItemDefinition>(entry.Item);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                LogItemFailed(entry.Item, exception.Message);
                continue;
            }

            Merge(stacks, item, entry.Quantity, changed);
        }

        ActiveInventory active = new() { Stacks = stacks, Money = Math.Max(0, inventory.Money), ApplyCarryLimits = inventory.ApplyCarryLimits };
        _world.Add(entity, active);
    }

    private SavedInventory? Save(Entity entity)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.TryGet(entity, out ActiveInventory inventory))
        {
            return null;
        }

        SavedInventory saved = new() { Money = inventory.Money };
        foreach (ItemStack stack in inventory.Stacks)
        {
            SavedStack? savedStack = SavedStack.From(stack, data);
            if (savedStack is not null)
            {
                saved.Stacks.Add(savedStack);
            }
        }

        return saved;
    }

    private void Restore(Entity entity, SavedInventory saved)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.Has<ActiveInventory>(entity))
        {
            return;
        }

        List<ItemStack> stacks = [];
        foreach (SavedStack savedStack in saved.Stacks)
        {
            ItemStack? stack = savedStack.ToStack(data);
            if (stack is not null)
            {
                stacks.Add(stack);
            }
        }

        ref ActiveInventory inventory = ref _world.Get<ActiveInventory>(entity);
        inventory.Stacks = stacks;
        inventory.Money = saved.Money;
        _events.Send(new CapacityChangedEvent(entity));
    }

    private void Deactivate(Entity entity)
    {
        if (_world.Has<ActiveInventory>(entity))
        {
            _world.Remove<ActiveInventory>(entity);
        }
    }

    private bool TryGetInventory(Entity owner, out ActiveInventory inventory)
    {
        if (!_world.IsAlive(owner))
        {
            inventory = default;
            return false;
        }

        return _world.TryGet(owner, out inventory);
    }

    private List<ItemStack>? FindStacks(Entity owner)
    {
        if (!TryGetInventory(owner, out ActiveInventory inventory))
        {
            return null;
        }

        return inventory.Stacks;
    }

    private void SetStackQuantity(Entity owner, Guid id, int quantity)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null || stack.Quantity == quantity)
        {
            return;
        }

        if (quantity <= 0)
        {
            List<ItemStack> stacks = FindStacks(owner)!;
            stacks.Remove(stack);
        }
        else
        {
            stack.Quantity = quantity;
        }

        _events.Send(new StackChangedEvent(owner, id));
    }

    private void SendItemChanged(Entity owner, ItemDefinition item, int oldTotal)
    {
        int newTotal = GetQuantity(owner, item);
        _events.Send(new ItemChangedEvent(owner, item, oldTotal, newTotal));
        _events.Send(new CapacityChangedEvent(owner));
    }

    private ConditionContext CreateContext(Entity owner)
    {
        WorldEntityView view = new(_world, owner);
        return new ConditionContext(_facts) { Instigator = view, Target = view, Services = _services };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "An inventory starts with items but there are no data assets to load them from; it starts empty.")]
    private partial void LogNoDataAssets();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The item {Item} can't be read, so the inventory starts without it: {Reason}")]
    private partial void LogItemFailed(string item, string reason);
}
