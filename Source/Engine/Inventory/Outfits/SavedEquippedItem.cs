using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>An item worn in a slot as a save keeps it: its file and the carried stack worn (<see cref="Guid.Empty"/> for none).</summary>
public sealed class SavedEquippedItem
{
    public Tag Slot { get; set; }

    public string Item { get; set; } = string.Empty;

    public Guid Stack { get; set; }
}
