using Arch.Core;

using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>An item was used from an inventory.</summary>
public readonly record struct ItemUsedEvent(Entity Owner, ItemDefinition Item);
