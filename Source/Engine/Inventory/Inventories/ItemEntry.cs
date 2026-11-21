using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Units of an item an inventory starts with.</summary>
public sealed class ItemEntry
{
    /// <summary>The <see cref="Items.ItemDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
}
