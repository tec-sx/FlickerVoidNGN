using Arch.Core;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>A pickup's item was used where it lies.</summary>
public readonly record struct UsedInPlaceEvent(Entity Pickup, Entity User);
