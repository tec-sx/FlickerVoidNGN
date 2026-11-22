namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>What a save keeps of what an entity wears: each slot's item file and the carried stack worn.</summary>
public sealed class SavedEquipment
{
    public List<SavedEquippedItem> Items { get; set; } = [];
}
