using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>Items and money rolled from a loot table: one plain stack per item, without ids.</summary>
public sealed record LootResult(List<ItemStack> Stacks, int Money);
