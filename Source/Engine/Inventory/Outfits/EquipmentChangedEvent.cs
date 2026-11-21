using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>What a slot holds changed; an empty slot's item is null.</summary>
public readonly record struct EquipmentChangedEvent(Entity Owner, Tag Slot, ItemDefinition? OldItem, ItemDefinition? NewItem);
