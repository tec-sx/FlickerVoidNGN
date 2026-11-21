using Arch.Core;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>An inventory's money changed.</summary>
public readonly record struct MoneyChangedEvent(Entity Owner, int OldMoney, int NewMoney);
