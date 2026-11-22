using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Outfits;
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

    /// <summary>How many of <paramref name="quantity"/> units fit, limited by weight and space; key items and inventories without limits take any amount.</summary>
    public int GetAcceptedQuantity(Entity owner, ItemDefinition item, int quantity = 1)
    {
        if (quantity <= 0 || !TryGetInventory(owner, out ActiveInventory inventory))
        {
            return 0;
        }

        if (!inventory.ApplyCarryLimits || Settings.IsKeyItem(item))
        {
            return quantity;
        }

        int accepted = quantity;
        if (Settings.EnforceWeight && item.Weight > 0)
        {
            float weightRoom = GetWeightLimit(owner) - GetTotalWeight(owner);
            int fitting = (int)MathF.Floor(weightRoom / item.Weight);
            accepted = Math.Clamp(fitting, 0, accepted);
        }

        if (accepted <= 0 || !Settings.EnforceSpace)
        {
            return accepted;
        }

        // Units first fill existing stacks for free; every new stack claims the item's cells.
        int maxPerStack = item.MaxQuantity;
        long room = 0;
        foreach (ItemStack stack in inventory.Stacks)
        {
            if (!CanMerge(stack, item))
            {
                continue;
            }

            if (maxPerStack <= 0)
            {
                return accepted;
            }

            room += Math.Max(0, maxPerStack - stack.Quantity);
        }

        if (accepted <= room)
        {
            return accepted;
        }

        int freeCells = Math.Max(0, GetCellCapacity(owner) - GetUsedCells(owner));
        int newStacks = freeCells / item.CellCount;
        long newRoom;
        if (maxPerStack > 0)
        {
            newRoom = (long)newStacks * maxPerStack;
        }
        else if (newStacks > 0)
        {
            newRoom = accepted;
        }
        else
        {
            newRoom = 0;
        }

        return (int)Math.Min(accepted, room + newRoom);
    }

    public bool CanAddItem(Entity owner, ItemDefinition item, int quantity = 1) => GetAcceptedQuantity(owner, item, quantity) >= quantity;

    /// <summary>Adds the units that fit the weight and space limits and returns how many that was.</summary>
    public int AddItem(Entity owner, ItemDefinition item, int quantity = 1)
    {
        int accepted = GetAcceptedQuantity(owner, item, quantity);
        return AddUnits(owner, item, accepted);
    }

    /// <summary>Adds every unit, ignoring weight and space, for what the story grants (quest rewards, story items), so it is never lost.</summary>
    public int AddStoryItem(Entity owner, ItemDefinition item, int quantity = 1)
    {
        if (!TryGetInventory(owner, out _))
        {
            return 0;
        }

        return AddUnits(owner, item, quantity);
    }

    /// <summary>Adds a copy of the whole stack as a new entry, keeping its instance data; its id, or <see cref="Guid.Empty"/> when it doesn't fit.</summary>
    public Guid AddStack(Entity owner, ItemStack stack)
    {
        if (!CanAcceptStack(owner, stack))
        {
            return Guid.Empty;
        }

        List<ItemStack> stacks = FindStacks(owner)!;
        int oldTotal = GetQuantity(owner, stack.Item);
        ItemStack added = stack.Copy();
        added.Id = Guid.NewGuid();
        stacks.Add(added);
        _events.Send(new StackChangedEvent(owner, added.Id));
        SendItemChanged(owner, stack.Item, oldTotal);
        return added.Id;
    }

    /// <summary>
    /// Takes as much of a stack as fits and returns the units taken: a partly used unit first and alone, plain units merged with what
    /// is there, and units with instance data as whole stacks of at most the item's stack size.
    /// </summary>
    public int Receive(Entity owner, ItemStack stack)
    {
        if (stack.Quantity <= 0 || !TryGetInventory(owner, out _))
        {
            return 0;
        }

        ItemStack rest = stack.Copy();
        int taken = 0;
        if (rest.Quantity > 1 && rest.FindData<ChargesLeft>() is not null)
        {
            ItemStack used = rest.Copy();
            used.Quantity = 1;
            Guid usedId = AddStack(owner, used);
            if (usedId == Guid.Empty)
            {
                return 0;
            }

            taken = 1;
            rest.Quantity--;
            rest.RemoveData<ChargesLeft>();
        }

        if (rest.IsPlain)
        {
            int added = AddItem(owner, rest.Item, rest.Quantity);
            return taken + added;
        }

        int maxPerStack = rest.Item.MaxQuantity;
        while (rest.Quantity > 0)
        {
            ItemStack part = rest.Copy();
            if (maxPerStack > 0)
            {
                part.Quantity = Math.Min(rest.Quantity, maxPerStack);
            }

            Guid partId = AddStack(owner, part);
            if (partId == Guid.Empty)
            {
                break;
            }

            taken += part.Quantity;
            rest.Quantity -= part.Quantity;
        }

        return taken;
    }

    /// <summary>
    /// Removes units from plain stacks before customized ones, and worn ones last; removes nothing and returns false when there aren't
    /// enough, or when taking off a worn bag would leave more carried than fits.
    /// </summary>
    public bool RemoveItem(Entity owner, ItemDefinition item, int quantity = 1)
    {
        if (quantity <= 0 || GetQuantity(owner, item) < quantity)
        {
            return false;
        }

        IEnumerable<ItemStack> stacks = GetStacks(owner);
        List<ItemStack> candidates = stacks.Where(stack => stack.Item == item).OrderBy(stack => GetRemovalPriority(owner, stack)).ToList();
        List<(Guid Id, int Quantity)> plan = [];
        int left = quantity;
        foreach (ItemStack stack in candidates)
        {
            if (left <= 0)
            {
                break;
            }

            int taken = Math.Min(left, stack.Quantity);
            if (!CanRemoveFromStack(owner, stack, taken))
            {
                return false;
            }

            plan.Add((stack.Id, stack.Quantity - taken));
            left -= taken;
        }

        int oldTotal = GetQuantity(owner, item);
        foreach ((Guid id, int newQuantity) in plan)
        {
            SetStackQuantity(owner, id, newQuantity);
        }

        SendItemChanged(owner, item, oldTotal);
        return true;
    }

    /// <summary>Removes units of one stack; removes nothing and returns false when it holds fewer, or when it is a worn bag whose room is needed.</summary>
    public bool RemoveStack(Entity owner, Guid id, int quantity = 1)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null || quantity <= 0 || stack.Quantity < quantity || !CanRemoveFromStack(owner, stack, quantity))
        {
            return false;
        }

        int oldTotal = GetQuantity(owner, stack.Item);
        SetStackQuantity(owner, id, stack.Quantity - quantity);
        SendItemChanged(owner, stack.Item, oldTotal);
        return true;
    }

    /// <summary>Replaces the instance data of the stack with <paramref name="id"/>.</summary>
    public bool SetInstanceData(Entity owner, Guid id, IEnumerable<ItemInstanceData> data)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null)
        {
            return false;
        }

        stack.Data = [.. data];
        _events.Send(new StackChangedEvent(owner, id));
        return true;
    }

    /// <summary>A stack of the item can be used: see <see cref="CanUseStack"/>.</summary>
    public bool CanUseItem(Entity owner, ItemDefinition item)
    {
        Guid id = FindStackToUse(owner, item);
        return CanUseStack(owner, id);
    }

    /// <summary>Uses the item from one of its stacks: a partly used unit first, else a plain stack. See <see cref="UseStack"/>.</summary>
    public bool UseItem(Entity owner, ItemDefinition item)
    {
        Guid id = FindStackToUse(owner, item);
        return UseStack(owner, id);
    }

    /// <summary>The stack's item is usable and its use conditions hold for the owner.</summary>
    public bool CanUseStack(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        UsableFragment? usable = stack?.Item.FindFragment<UsableFragment>();
        if (stack is null || usable is null)
        {
            return false;
        }

        // A unit this use would use up must be able to leave the inventory.
        ItemStack spent = stack.Copy();
        if (spent.SpendUse() && !CanRemoveFromStack(owner, stack, 1))
        {
            return false;
        }

        ConditionContext context = CreateContext(owner);
        return usable.UseConditions.Evaluate(context);
    }

    /// <summary>
    /// Applies the item's usable effects to the owner. With charges one use is spent (a partly used unit becomes its own stack) and the
    /// unit goes after its last use; without, one unit is consumed when the item is consumed on use.
    /// </summary>
    public bool UseStack(Entity owner, Guid id)
    {
        if (!CanUseStack(owner, id))
        {
            return false;
        }

        ItemDefinition item = FindStack(owner, id)!.Item;
        UsableFragment usable = item.FindFragment<UsableFragment>()!;
        ConditionContext context = CreateContext(owner);
        usable.Effects.Apply(context);

        // Effects may have changed the inventory, so the stack is looked up again.
        SpendUse(owner, id);
        _events.Send(new ItemUsedEvent(owner, item));
        return true;
    }

    /// <summary>Uses left on the stack's next unit; 0 when there is no such stack.</summary>
    public int GetCharges(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null)
        {
            return 0;
        }

        return stack.UsesLeft;
    }

    /// <summary>
    /// Moves up to <paramref name="quantity"/> units of an unworn stack to another inventory, keeping its instance data (a stack with instance
    /// data moves whole, up to what one stack holds, or not at all); the units moved.
    /// </summary>
    public int TransferStack(Entity from, Entity to, Guid id, int quantity)
    {
        return MoveStack(from, to, id, quantity, false, Tag.None);
    }

    /// <summary>Like <see cref="TransferStack"/>, but the moved units are marked stolen from <paramref name="stolenFrom"/> (none when unknown).</summary>
    public int TransferStackAsStolen(Entity from, Entity to, Guid id, int quantity, Tag stolenFrom)
    {
        return MoveStack(from, to, id, quantity, true, stolenFrom);
    }

    /// <summary>Moves every unworn stack, and the money when asked, to another inventory; a valid <paramref name="stolenFrom"/> marks the items stolen. The stacks moved whole.</summary>
    public int TransferAll(Entity from, Entity to, bool includeMoney = true, Tag stolenFrom = default)
    {
        if (from == to || !TryGetInventory(to, out _))
        {
            return 0;
        }

        IEnumerable<ItemStack> stacks = GetStacks(from);
        List<(Guid Id, int Quantity)> moving = stacks.Where(stack => !IsWorn(from, stack.Id)).Select(static stack => (stack.Id, stack.Quantity)).ToList();
        int movedStacks = 0;
        foreach ((Guid id, int quantity) in moving)
        {
            int moved = MoveStack(from, to, id, quantity, stolenFrom.IsValid, stolenFrom);
            if (moved >= quantity)
            {
                movedStacks++;
            }
        }

        if (includeMoney)
        {
            int money = GetMoney(from);
            TransferMoney(from, to, money);
        }

        return movedStacks;
    }

    /// <summary>Money is a number per character, not an item.</summary>
    public int GetMoney(Entity owner)
    {
        if (!TryGetInventory(owner, out ActiveInventory inventory))
        {
            return 0;
        }

        return inventory.Money;
    }

    public bool CanAfford(Entity owner, int amount) => GetMoney(owner) >= amount;

    /// <summary>Ignores amounts of 0 or less.</summary>
    public void AddMoney(Entity owner, int amount)
    {
        if (amount <= 0 || !TryGetInventory(owner, out _))
        {
            return;
        }

        ref ActiveInventory inventory = ref _world.Get<ActiveInventory>(owner);
        int oldMoney = inventory.Money;
        long sum = (long)oldMoney + amount;
        inventory.Money = (int)Math.Min(sum, int.MaxValue);
        _events.Send(new MoneyChangedEvent(owner, oldMoney, inventory.Money));
    }

    /// <summary>Removes nothing and returns false when there isn't enough.</summary>
    public bool RemoveMoney(Entity owner, int amount)
    {
        if (amount < 0 || !TryGetInventory(owner, out ActiveInventory current) || current.Money < amount)
        {
            return false;
        }

        if (amount > 0)
        {
            ref ActiveInventory inventory = ref _world.Get<ActiveInventory>(owner);
            int oldMoney = inventory.Money;
            inventory.Money -= amount;
            _events.Send(new MoneyChangedEvent(owner, oldMoney, inventory.Money));
        }

        return true;
    }

    /// <summary>Moves <paramref name="amount"/>, or all there is when less, to another inventory; the amount moved.</summary>
    public int TransferMoney(Entity from, Entity to, int amount)
    {
        int money = GetMoney(from);
        int moved = Math.Min(amount, money);
        if (from == to || moved <= 0 || !TryGetInventory(to, out _))
        {
            return 0;
        }

        RemoveMoney(from, moved);
        AddMoney(to, moved);
        return moved;
    }

    /// <summary>Kilograms carried; key items weigh nothing.</summary>
    public float GetTotalWeight(Entity owner)
    {
        float total = 0;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (!Settings.IsKeyItem(stack.Item))
            {
                total += stack.Item.Weight * stack.Quantity;
            }
        }

        return total;
    }

    /// <summary>The settings' base limit plus worn bags plus the bonus the game grants.</summary>
    public float GetWeightLimit(Entity owner)
    {
        float bonus = 0;
        if (TryGetInventory(owner, out ActiveInventory inventory))
        {
            bonus = inventory.WeightBonus;
        }

        foreach (ContainerFragment container in GetWornContainers(owner))
        {
            bonus += container.WeightBonus;
        }

        return Settings.BaseWeightLimit + bonus;
    }

    /// <summary>Cells the stacks claim; key items claim none.</summary>
    public int GetUsedCells(Entity owner)
    {
        int used = 0;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (!Settings.IsKeyItem(stack.Item))
            {
                used += stack.Item.CellCount;
            }
        }

        return used;
    }

    /// <summary>The settings' pockets plus worn bags plus the bonus the game grants.</summary>
    public int GetCellCapacity(Entity owner)
    {
        int bonus = 0;
        if (TryGetInventory(owner, out ActiveInventory inventory))
        {
            bonus = inventory.CellBonus;
        }

        foreach (ContainerFragment container in GetWornContainers(owner))
        {
            bonus += container.Cells;
        }

        return Settings.BasePocketCells + bonus;
    }

    /// <summary>
    /// Whether what is carried still fits after the limits change by <paramref name="weightLimitChange"/> and <paramref name="cellCapacityChange"/>
    /// and the load by <paramref name="weightChange"/> and <paramref name="usedCellChange"/>; only a lowered limit is checked.
    /// </summary>
    public bool FitsAfterChange(Entity owner, float weightLimitChange, int cellCapacityChange, float weightChange = 0, int usedCellChange = 0)
    {
        if (!TryGetInventory(owner, out ActiveInventory inventory) || !inventory.ApplyCarryLimits)
        {
            return true;
        }

        float weight = GetTotalWeight(owner) + weightChange;
        float weightLimit = GetWeightLimit(owner) + weightLimitChange;
        if (weightLimitChange < 0 && Settings.EnforceWeight && weight > weightLimit + WeightTolerance)
        {
            return false;
        }

        int cells = GetUsedCells(owner) + usedCellChange;
        int capacity = GetCellCapacity(owner) + cellCapacityChange;
        return cellCapacityChange >= 0 || !Settings.EnforceSpace || cells <= capacity;
    }

    /// <summary>Sets the extra allowance the game grants, such as from an Athletics attribute.</summary>
    public void SetBonuses(Entity owner, float weightBonus, int cellBonus)
    {
        if (!TryGetInventory(owner, out _))
        {
            return;
        }

        ref ActiveInventory inventory = ref _world.Get<ActiveInventory>(owner);
        if (inventory.WeightBonus == weightBonus && inventory.CellBonus == cellBonus)
        {
            return;
        }

        inventory.WeightBonus = weightBonus;
        inventory.CellBonus = cellBonus;
        _events.Send(new CapacityChangedEvent(owner));
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

    private bool IsWorn(Entity owner, Guid id)
    {
        return _world.TryGet(owner, out ActiveEquipment equipment) && equipment.IsWorn(id);
    }

    private List<ContainerFragment> GetWornContainers(Entity owner)
    {
        List<ContainerFragment> containers = [];
        if (!_world.IsAlive(owner) || !_world.TryGet(owner, out ActiveEquipment equipment))
        {
            return containers;
        }

        foreach (EquippedItem worn in equipment.Items)
        {
            ContainerFragment? container = worn.Item.FindFragment<ContainerFragment>();
            if (container is not null)
            {
                containers.Add(container);
            }
        }

        return containers;
    }

    /// <summary>Plain stacks before customized ones, and worn ones last.</summary>
    private int GetRemovalPriority(Entity owner, ItemStack stack)
    {
        int priority = 0;
        if (IsWorn(owner, stack.Id))
        {
            priority += 2;
        }

        if (!stack.IsPlain)
        {
            priority++;
        }

        return priority;
    }

    /// <summary>Taking a whole worn bag off loses its room but also its own weight and cells, which must leave the rest fitting.</summary>
    private bool CanRemoveFromStack(Entity owner, ItemStack stack, int quantity)
    {
        ContainerFragment? container = stack.Item.FindFragment<ContainerFragment>();
        if (quantity < stack.Quantity || container is null || !IsWorn(owner, stack.Id))
        {
            return true;
        }

        float weight = stack.Item.Weight * stack.Quantity;
        return FitsAfterChange(owner, -container.WeightBonus, -container.Cells, -weight, -stack.Item.CellCount);
    }

    private bool CanAcceptStack(Entity owner, ItemStack stack)
    {
        ItemDefinition item = stack.Item;
        if (stack.Quantity <= 0 || (item.MaxQuantity > 0 && stack.Quantity > item.MaxQuantity))
        {
            return false;
        }

        if (!TryGetInventory(owner, out ActiveInventory inventory))
        {
            return false;
        }

        if (!inventory.ApplyCarryLimits || Settings.IsKeyItem(item))
        {
            return true;
        }

        float weight = GetTotalWeight(owner) + (item.Weight * stack.Quantity);
        if (Settings.EnforceWeight && weight > GetWeightLimit(owner) + WeightTolerance)
        {
            return false;
        }

        int cells = GetUsedCells(owner) + item.CellCount;
        return !Settings.EnforceSpace || cells <= GetCellCapacity(owner);
    }

    private int AddUnits(Entity owner, ItemDefinition item, int quantity)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        List<ItemStack> stacks = FindStacks(owner)!;
        int oldTotal = GetQuantity(owner, item);
        List<Guid> changed = [];
        Merge(stacks, item, quantity, changed);
        foreach (Guid id in changed)
        {
            _events.Send(new StackChangedEvent(owner, id));
        }

        SendItemChanged(owner, item, oldTotal);
        return quantity;
    }

    /// <summary>A partly used unit first, then plain stacks before customized ones, and worn ones last.</summary>
    private Guid FindStackToUse(Entity owner, ItemDefinition item)
    {
        ItemStack? best = null;
        int bestPriority = int.MaxValue;
        foreach (ItemStack stack in GetStacks(owner))
        {
            if (stack.Item != item)
            {
                continue;
            }

            int priority = 2;
            if (stack.FindData<ChargesLeft>() is not null)
            {
                priority = 0;
            }
            else if (stack.IsPlain)
            {
                priority = 1;
            }

            if (IsWorn(owner, stack.Id))
            {
                priority += 3;
            }

            if (priority < bestPriority)
            {
                best = stack;
                bestPriority = priority;
            }
        }

        if (best is null)
        {
            return Guid.Empty;
        }

        return best.Id;
    }

    private void SpendUse(Entity owner, Guid id)
    {
        ItemStack? stack = FindStack(owner, id);
        if (stack is null)
        {
            return;
        }

        bool charges = stack.Item.FindFragment<ChargesFragment>() is not null;
        ItemStack spent = stack.Copy();
        if (spent.SpendUse())
        {
            RemoveStack(owner, id, 1);
        }
        else if (charges && spent.Quantity > 1)
        {
            // The partly used unit keeps the stack's id, so whoever holds the id goes on draining it; the full units left become a stack of their own.
            ItemStack full = spent.Copy();
            full.RemoveData<ChargesLeft>();
            full.Id = Guid.NewGuid();
            full.Quantity = spent.Quantity - 1;
            stack.Data = spent.Data;
            stack.Quantity = 1;
            List<ItemStack> stacks = FindStacks(owner)!;
            stacks.Add(full);
            _events.Send(new StackChangedEvent(owner, id));
            _events.Send(new StackChangedEvent(owner, full.Id));
            _events.Send(new CapacityChangedEvent(owner));
            return;
        }

        // The unit's charges left changed, or were cleared from the full units left.
        ItemStack? rest = FindStack(owner, id);
        if (charges && rest is not null)
        {
            rest.Data = spent.Data;
            _events.Send(new StackChangedEvent(owner, id));
        }
    }

    private int MoveStack(Entity from, Entity to, Guid id, int quantity, bool markStolen, Tag stolenFrom)
    {
        ItemStack? stack = FindStack(from, id);
        if (from == to || stack is null || quantity <= 0 || IsWorn(from, id) || !TryGetInventory(to, out _))
        {
            return 0;
        }

        ItemStack moved = stack.Copy();
        moved.Quantity = Math.Min(quantity, stack.Quantity);
        if (markStolen)
        {
            moved.SetData(new Stolen(stolenFrom));
        }

        // Plain units merge and may move in part; a stack with instance data moves whole, up to what one stack holds.
        bool plain = moved.IsPlain;
        if (plain)
        {
            moved.Quantity = GetAcceptedQuantity(to, moved.Item, moved.Quantity);
        }
        else if (moved.Item.MaxQuantity > 0)
        {
            moved.Quantity = Math.Min(moved.Quantity, moved.Item.MaxQuantity);
        }

        if (moved.Quantity <= 0 || (!plain && !CanAcceptStack(to, moved)) || !RemoveStack(from, id, moved.Quantity))
        {
            return 0;
        }

        if (plain)
        {
            AddItem(to, moved.Item, moved.Quantity);
        }
        else
        {
            AddStack(to, moved);
        }

        return moved.Quantity;
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
