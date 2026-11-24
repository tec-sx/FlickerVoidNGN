using Arch.Core;

using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

public sealed partial class InventorySystem
{
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
}
