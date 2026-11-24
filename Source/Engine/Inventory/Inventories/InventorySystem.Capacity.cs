using Arch.Core;

using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Outfits;

namespace FlickerVoid.Engine.Inventory.Inventories;

public sealed partial class InventorySystem
{
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
}
