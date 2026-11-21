using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>An item worn in a slot from the start.</summary>
public sealed class EquipmentEntry
{
    public Tag Slot { get; set; }

    /// <summary>The <see cref="Items.ItemDefinition"/> data asset; a carried stack of it is worn.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;
}
