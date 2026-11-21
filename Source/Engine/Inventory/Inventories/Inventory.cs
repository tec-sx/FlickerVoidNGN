namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Items and money an entity carries, as it starts; while the game plays they are held by an <see cref="ActiveInventory"/>.</summary>
public struct Inventory
{
    public Inventory()
    {
    }

    public List<ItemEntry> Items { get; set; } = [];

    public int Money { get; set; }

    /// <summary>Weight and space limits apply; off for loot containers, merchants' stock and other storage.</summary>
    public bool ApplyCarryLimits { get; set; } = true;
}
