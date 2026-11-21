using Arch.Core;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>A stack was added, changed or removed (finding it fails for a removed one).</summary>
public readonly record struct StackChangedEvent(Entity Owner, Guid Stack);
