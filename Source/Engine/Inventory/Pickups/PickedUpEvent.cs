using Arch.Core;

namespace FlickerVoid.Engine.Inventory.Pickups;

/// <summary>Some or all of a pickup was taken; <paramref name="Taken"/> units went into the picker's inventory.</summary>
public readonly record struct PickedUpEvent(Entity Pickup, Entity Picker, int Taken);
