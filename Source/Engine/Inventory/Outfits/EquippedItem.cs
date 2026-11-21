using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>An item worn in a slot, and the carried stack worn (<see cref="Guid.Empty"/> for an item worn without being carried).</summary>
public sealed record EquippedItem(Tag Slot, ItemDefinition Item, Guid Stack);
