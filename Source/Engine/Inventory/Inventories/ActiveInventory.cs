namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>An inventory while the game plays: its stacks, money, whether limits apply, and the allowance granted on top of the settings.</summary>
public struct ActiveInventory
{
    public List<ItemStack> Stacks { get; set; }

    public int Money { get; set; }

    public bool ApplyCarryLimits { get; set; }

    /// <summary>Extra kilograms the game grants, such as from an attribute.</summary>
    public float WeightBonus { get; set; }

    /// <summary>Extra cells the game grants.</summary>
    public int CellBonus { get; set; }
}
