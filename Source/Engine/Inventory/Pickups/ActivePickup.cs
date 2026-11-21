using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>A pickup while the game plays: what is left of it as a stack (its instance data included, such as a dropped stack's), and whether it goes when empty.</summary>
public struct ActivePickup
{
    public ItemStack Stack { get; set; }

    public bool DestroyWhenEmpty { get; set; }

    public readonly bool IsEmpty => Stack.Quantity <= 0;
}
