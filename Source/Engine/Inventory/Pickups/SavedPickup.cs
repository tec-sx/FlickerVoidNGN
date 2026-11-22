using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>What a save keeps of a pickup: what is left of it.</summary>
public sealed class SavedPickup
{
    public SavedStack Stack { get; set; } = new();
}
