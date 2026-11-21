using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Taken without the owner's consent; the owner is a faction or character id, or none when unknown.</summary>
public sealed record Stolen(Tag Owner) : ItemInstanceData;
