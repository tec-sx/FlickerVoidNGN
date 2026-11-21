using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>Units of an item lying in the world to be picked up or used where they lie; while the game plays it is also an <see cref="ActivePickup"/>.</summary>
public struct Pickup
{
    public Pickup()
    {
    }

    /// <summary>The <see cref="Items.ItemDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    /// <summary>Destroy the entity once everything is taken or used up; otherwise it is left empty.</summary>
    public bool DestroyWhenEmpty { get; set; } = true;
}
