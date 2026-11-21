using Arch.Core;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>An inventory's weight, space or limits changed.</summary>
public readonly record struct CapacityChangedEvent(Entity Owner);
