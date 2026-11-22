using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>
/// Wears equippable items (outfits, accessories, bags), one per slot. An item goes into one of its slots and covers its blocked slots;
/// whatever conflicts comes off. The tags worn items grant are added to the wearer's <see cref="OwnedTags"/>, so conditions and AI read
/// disguises and dress codes, and a worn stack that leaves the inventory comes off.
/// </summary>
public sealed partial class EquipmentSystem
{
    private readonly World _world;
    private readonly EventBus _events;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly InventorySystem _inventories;
    private readonly ILogger<EquipmentSystem> _logger;

    public EquipmentSystem(Game game, InventorySystem inventories)
    {
        _world = game.World;
        _events = game.Events;
        _facts = game.Facts;
        _services = game.Services;
        _inventories = inventories;
        _logger = game.LoggerFactory.CreateLogger<EquipmentSystem>();
        ArrayRegistry.Add<ActiveEquipment>();
        ArrayRegistry.Add<OwnedTags>();
        game.Lifecycle.OnActivate((Entity entity, ref Equipment equipment) => Activate(entity, equipment));
        game.Lifecycle.OnDeactivate((Entity entity, ref Equipment _) => Deactivate(entity));
        game.Events.Handle((in StackChangedEvent changed) => TakeOffIfGone(changed.Owner, changed.Stack));
        game.SaveStates.Add<SavedEquipment>("equipment", Save, Restore);
    }

    /// <summary>The distinct tags the worn items grant.</summary>
    public static List<Tag> GetGrantedTags(ActiveEquipment equipment)
    {
        List<Tag> tags = [];
        foreach (EquippedItem worn in equipment.Items)
        {
            EquippableFragment? equippable = worn.Item.FindFragment<EquippableFragment>();
            if (equippable is null)
            {
                continue;
            }

            foreach (Tag tag in equippable.GrantedTags)
            {
                if (!tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }
        }

        return tags;
    }

    public IReadOnlyList<EquippedItem> GetAllEquipped(Entity owner)
    {
        if (!TryGetEquipment(owner, out ActiveEquipment equipment))
        {
            return [];
        }

        return equipment.Items;
    }

    public ItemDefinition? GetEquipped(Entity owner, Tag slot)
    {
        EquippedItem? worn = FindEntry(owner, slot);
        return worn?.Item;
    }

    /// <summary>The stack worn in the slot; <see cref="Guid.Empty"/> when the slot is empty or holds an item worn without being carried.</summary>
    public Guid GetEquippedStack(Entity owner, Tag slot)
    {
        EquippedItem? worn = FindEntry(owner, slot);
        if (worn is null)
        {
            return Guid.Empty;
        }

        return worn.Stack;
    }

    /// <summary>The worn item whose blocked slots cover the slot, if any.</summary>
    public ItemDefinition? GetBlockingItem(Entity owner, Tag slot)
    {
        foreach (EquippedItem worn in GetAllEquipped(owner))
        {
            EquippableFragment? equippable = worn.Item.FindFragment<EquippableFragment>();
            if (equippable is not null && equippable.BlockedSlots.Contains(slot))
            {
                return worn.Item;
            }
        }

        return null;
    }

    public bool IsEquipped(Entity owner, ItemDefinition item)
    {
        IEnumerable<EquippedItem> worn = GetAllEquipped(owner);
        return worn.Any(entry => entry.Item == item);
    }

    public bool IsStackEquipped(Entity owner, Guid stack)
    {
        return TryGetEquipment(owner, out ActiveEquipment equipment) && equipment.IsWorn(stack);
    }

    public bool CanEquip(Entity owner, ItemDefinition item)
    {
        return TryGetEquipment(owner, out ActiveEquipment equipment)
            && TryResolveStack(owner, equipment, item, out Guid stack)
            && TryPlan(owner, equipment, item, stack, Tag.None, out _, out _);
    }

    /// <summary>Wears a carried, unworn stack of the item in its first free slot, taking off whatever conflicts.</summary>
    public bool Equip(Entity owner, ItemDefinition item)
    {
        if (!TryGetEquipment(owner, out ActiveEquipment equipment)
            || !TryResolveStack(owner, equipment, item, out Guid stack)
            || !TryPlan(owner, equipment, item, stack, Tag.None, out Tag slot, out List<Tag> removed))
        {
            return false;
        }

        Apply(owner, slot, item, stack, removed);
        return true;
    }

    /// <summary>With no slot, the first of the item's slots that is free.</summary>
    public bool CanEquipStack(Entity owner, Guid stack, Tag slot = default)
    {
        ItemStack? carried = _inventories.FindStack(owner, stack);
        return carried is not null
            && TryGetEquipment(owner, out ActiveEquipment equipment)
            && TryPlan(owner, equipment, carried.Item, stack, slot, out _, out _);
    }

    /// <summary>Wears a carried stack; with no slot, in the first of its item's slots that is free, and with one a worn stack moves there.</summary>
    public bool EquipStack(Entity owner, Guid stack, Tag slot = default)
    {
        ItemStack? carried = _inventories.FindStack(owner, stack);
        if (carried is null
            || !TryGetEquipment(owner, out ActiveEquipment equipment)
            || !TryPlan(owner, equipment, carried.Item, stack, slot, out Tag chosen, out List<Tag> removed))
        {
            return false;
        }

        Apply(owner, chosen, carried.Item, stack, removed);
        return true;
    }

    /// <summary>False when the slot is empty, or when taking off a bag would leave more carried than fits.</summary>
    public bool CanUnequip(Entity owner, Tag slot)
    {
        return GetEquipped(owner, slot) is not null && FitsCapacity(owner, [slot], null);
    }

    public bool Unequip(Entity owner, Tag slot)
    {
        if (!CanUnequip(owner, slot))
        {
            return false;
        }

        Apply(owner, slot, null, Guid.Empty, []);
        return true;
    }

    private static bool Conflicts(EquippedItem worn, Tag slot, EquippableFragment added)
    {
        if (worn.Slot == slot || added.BlockedSlots.Contains(worn.Slot))
        {
            return true;
        }

        EquippableFragment? wornEquippable = worn.Item.FindFragment<EquippableFragment>();
        return wornEquippable is not null && wornEquippable.BlockedSlots.Contains(slot);
    }

    private static List<Tag> CollectConflicts(ActiveEquipment equipment, Tag slot, EquippableFragment added, Guid stack)
    {
        List<Tag> conflicts = [];
        foreach (EquippedItem worn in equipment.Items)
        {
            // Moving a worn stack to another slot takes it off its current one.
            bool sameStack = stack != Guid.Empty && worn.Stack == stack;
            if (Conflicts(worn, slot, added) || sameStack)
            {
                conflicts.Add(worn.Slot);
            }
        }

        return conflicts;
    }

    private static bool HasContainer(ItemDefinition? item) => item?.FindFragment<ContainerFragment>() is not null;

    private void Activate(Entity entity, Equipment equipment)
    {
        ActiveEquipment active = new() { Items = [], RequireInventory = equipment.RequireInventory, AppliedTags = [] };
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null && equipment.Equipped.Count > 0)
        {
            LogNoDataAssets();
        }

        foreach (EquipmentEntry entry in equipment.Equipped)
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

            // Starting items are linked to carried stacks; one that isn't carried is worn without a stack.
            TryResolveStack(entity, active, item, out Guid stack);
            active.Items.Add(new EquippedItem(entry.Slot, item, stack));
        }

        _world.Add(entity, active);
        RefreshGrantedTags(entity);
    }

    private void Deactivate(Entity entity)
    {
        if (!_world.Has<ActiveEquipment>(entity))
        {
            return;
        }

        ActiveEquipment equipment = _world.Get<ActiveEquipment>(entity);
        equipment.Items.Clear();
        RefreshGrantedTags(entity);
        _world.Remove<ActiveEquipment>(entity);
    }

    private bool TryGetEquipment(Entity owner, out ActiveEquipment equipment)
    {
        if (!_world.IsAlive(owner))
        {
            equipment = default;
            return false;
        }

        return _world.TryGet(owner, out equipment);
    }

    private EquippedItem? FindEntry(Entity owner, Tag slot)
    {
        IEnumerable<EquippedItem> worn = GetAllEquipped(owner);
        return worn.FirstOrDefault(entry => entry.Slot == slot);
    }

    /// <summary>An unworn carried stack of the item; without one, true only when items may be worn without being carried.</summary>
    private bool TryResolveStack(Entity owner, ActiveEquipment equipment, ItemDefinition item, out Guid stack)
    {
        foreach (ItemStack carried in _inventories.GetStacks(owner))
        {
            if (carried.Item == item && !equipment.IsWorn(carried.Id))
            {
                stack = carried.Id;
                return true;
            }
        }

        stack = Guid.Empty;
        return !equipment.RequireInventory;
    }

    private bool TryPlan(Entity owner, ActiveEquipment equipment, ItemDefinition item, Guid stack, Tag requested, out Tag slot, out List<Tag> removed)
    {
        slot = Tag.None;
        removed = [];
        EquippableFragment? equippable = item.FindFragment<EquippableFragment>();
        if (equippable is null || equippable.Slots.Count == 0)
        {
            return false;
        }

        // Without a stack to tell copies apart, one worn copy per item. A worn stack moves only to a named slot.
        bool wornCopy = stack == Guid.Empty && equipment.Items.Exists(worn => worn.Item == item);
        bool wornStack = !requested.IsValid && equipment.IsWorn(stack);
        if (wornCopy || wornStack)
        {
            return false;
        }

        List<Tag> candidates;
        if (requested.IsValid)
        {
            if (!equippable.Slots.Contains(requested))
            {
                return false;
            }

            candidates = [requested];
        }
        else
        {
            candidates = equippable.Slots;
        }

        slot = candidates[0];
        removed = CollectConflicts(equipment, slot, equippable, stack);
        foreach (Tag candidate in candidates)
        {
            List<Tag> conflicting = CollectConflicts(equipment, candidate, equippable, stack);
            if (conflicting.Count == 0)
            {
                slot = candidate;
                removed = [];
                break;
            }
        }

        Tag chosen = slot;
        EquippedItem? current = equipment.Items.Find(worn => worn.Slot == chosen);
        if (current is not null && current.Item == item && current.Stack == stack)
        {
            return false;
        }

        WorldEntityView view = new(_world, owner);
        ConditionContext context = new(_facts) { Instigator = view, Target = view, Services = _services };
        return FitsCapacity(owner, removed, item) && equippable.EquipConditions.Evaluate(context);
    }

    /// <summary>Whether what is carried still fits after the bags in <paramref name="removed"/> come off and <paramref name="added"/> goes on.</summary>
    private bool FitsCapacity(Entity owner, List<Tag> removed, ItemDefinition? added)
    {
        float weightLimitChange = 0;
        int cellChange = 0;
        foreach (Tag slot in removed)
        {
            ItemDefinition? item = GetEquipped(owner, slot);
            ContainerFragment? container = item?.FindFragment<ContainerFragment>();
            if (container is not null)
            {
                weightLimitChange -= container.WeightBonus;
                cellChange -= container.Cells;
            }
        }

        ContainerFragment? addedContainer = added?.FindFragment<ContainerFragment>();
        if (addedContainer is not null)
        {
            weightLimitChange += addedContainer.WeightBonus;
            cellChange += addedContainer.Cells;
        }

        return _inventories.FitsAfterChange(owner, weightLimitChange, cellChange);
    }

    private SavedEquipment? Save(Entity entity)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.TryGet(entity, out ActiveEquipment equipment))
        {
            return null;
        }

        SavedEquipment saved = new();
        foreach (EquippedItem worn in equipment.Items)
        {
            if (data.TryGetPath(worn.Item, out string path))
            {
                saved.Items.Add(new SavedEquippedItem { Slot = worn.Slot, Item = path, Stack = worn.Stack });
            }
        }

        return saved;
    }

    /// <summary>Takes off what is worn and wears what the save says, slot by slot, so granted tags and capacity follow.</summary>
    private void Restore(Entity entity, SavedEquipment saved)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || !_world.TryGet(entity, out ActiveEquipment equipment))
        {
            return;
        }

        List<Tag> worn = equipment.Items.Select(static item => item.Slot).ToList();
        foreach (Tag slot in worn)
        {
            Apply(entity, slot, null, Guid.Empty, []);
        }

        foreach (SavedEquippedItem savedItem in saved.Items)
        {
            ItemDefinition item;
            try
            {
                item = data.Load<ItemDefinition>(savedItem.Item);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                continue;
            }

            Apply(entity, savedItem.Slot, item, savedItem.Stack, []);
        }
    }

    private void Apply(Entity owner, Tag slot, ItemDefinition? item, Guid stack, List<Tag> removed)
    {
        List<EquippedItem> items = _world.Get<ActiveEquipment>(owner).Items;
        List<EquipmentChangedEvent> changes = [];
        foreach (Tag removedSlot in removed)
        {
            if (removedSlot == slot)
            {
                continue;
            }

            ItemDefinition? old = GetEquipped(owner, removedSlot);
            changes.Add(new EquipmentChangedEvent(owner, removedSlot, old, null));
            items.RemoveAll(worn => worn.Slot == removedSlot);
        }

        ItemDefinition? replaced = GetEquipped(owner, slot);
        changes.Add(new EquipmentChangedEvent(owner, slot, replaced, item));
        items.RemoveAll(worn => worn.Slot == slot);
        if (item is not null)
        {
            items.Add(new EquippedItem(slot, item, stack));
        }

        RefreshGrantedTags(owner);
        bool capacityChanged = false;
        foreach (EquipmentChangedEvent change in changes)
        {
            _events.Send(change);
            capacityChanged |= HasContainer(change.OldItem) || HasContainer(change.NewItem);
        }

        if (capacityChanged)
        {
            _events.Send(new CapacityChangedEvent(owner));
        }
    }

    /// <summary>Swaps the tags applied before for those the worn items grant now.</summary>
    private void RefreshGrantedTags(Entity owner)
    {
        ref ActiveEquipment equipment = ref _world.Get<ActiveEquipment>(owner);
        List<Tag> granted = GetGrantedTags(equipment);
        List<Tag> previous = equipment.AppliedTags;
        equipment.AppliedTags = granted;
        GrantedTags.Replace(_world, owner, previous, granted);
    }

    /// <summary>A worn stack that left the inventory comes off, or with items allowed without being carried, stays worn without its stack.</summary>
    private void TakeOffIfGone(Entity owner, Guid stack)
    {
        if (!TryGetEquipment(owner, out ActiveEquipment equipment) || _inventories.FindStack(owner, stack) is not null)
        {
            return;
        }

        int index = equipment.Items.FindIndex(worn => worn.Stack == stack);
        if (index < 0)
        {
            return;
        }

        EquippedItem worn = equipment.Items[index];
        if (equipment.RequireInventory)
        {
            Apply(owner, worn.Slot, null, Guid.Empty, []);
        }
        else
        {
            equipment.Items[index] = worn with { Stack = Guid.Empty };
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Equipment starts with items but there are no data assets to load them from; nothing is worn.")]
    private partial void LogNoDataAssets();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The item {Item} can't be read, so it isn't worn: {Reason}")]
    private partial void LogItemFailed(string item, string reason);
}
