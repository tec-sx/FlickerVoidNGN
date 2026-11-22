using Arch.Core;

using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Units of an item were added to or removed from an inventory; the quantities are totals across its stacks.</summary>
public readonly record struct ItemChangedEvent(Entity Owner, ItemDefinition Item, int OldQuantity, int NewQuantity);
