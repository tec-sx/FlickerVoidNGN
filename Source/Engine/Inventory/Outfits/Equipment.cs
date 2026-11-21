namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>What an entity wears, as it starts; while the game plays it is an <see cref="ActiveEquipment"/>.</summary>
public struct Equipment
{
    public Equipment()
    {
    }

    public List<EquipmentEntry> Equipped { get; set; } = [];

    /// <summary>Only items carried in the entity's inventory can be worn.</summary>
    public bool RequireInventory { get; set; } = true;
}
