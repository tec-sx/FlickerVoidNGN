namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>What a save keeps of an inventory: its stacks and money.</summary>
public sealed class SavedInventory
{
    public List<SavedStack> Stacks { get; set; } = [];

    public int Money { get; set; }
}
