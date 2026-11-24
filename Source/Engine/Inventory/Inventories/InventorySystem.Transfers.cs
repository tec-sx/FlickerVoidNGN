using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

public sealed partial class InventorySystem
{
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
}
