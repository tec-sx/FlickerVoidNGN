using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Interface.Hub.Inventory;

/// <summary>An outfit slot as the inventory page lists it.</summary>
public sealed record OutfitSlot(Tag Slot, string Label);
