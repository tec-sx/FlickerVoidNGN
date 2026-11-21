using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Pickups;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Inventory.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Inventory;

/// <summary>Adds inventories to a game: the <c>item</c> data asset type and its fragments, the inventory conditions and effects, the <c>inventory</c> and <c>pickup</c> components, and the systems that run them.</summary>
public static class InventoryGameExtensions
{
    public static InventorySystem AddInventory(this Game game, InventorySettings? settings = null)
    {
        game.Data.AddResolver(InventoryJsonContext.Default);
        game.Data.Register<ItemDefinition>("item");
        game.Data.RegisterDerived<ItemFragment, UsableFragment>("usable");
        game.Data.RegisterDerived<ItemFragment, ChargesFragment>("charges");
        game.Data.RegisterDerived<ItemFragment, ValueFragment>("value");
        game.Data.RegisterDerived<Condition, HasItemCondition>("hasItem");
        game.Data.RegisterDerived<Condition, HasItemWithTagCondition>("hasItemWithTag");
        game.Data.RegisterDerived<Condition, HasMoneyCondition>("hasMoney");
        game.Data.RegisterDerived<Effect, GiveItemEffect>("giveItem");
        game.Data.RegisterDerived<Effect, TakeItemEffect>("takeItem");
        game.Data.RegisterDerived<Effect, GiveMoneyEffect>("giveMoney");
        game.Data.RegisterDerived<Effect, TakeMoneyEffect>("takeMoney");
        game.Data.RegisterDerived<Effect, PickUpEffect>("pickUp");
        game.Data.RegisterDerived<Effect, UseInPlaceEffect>("useInPlace");
        game.Spawner.Register("inventory", InventoryJsonContext.Default.Inventory);
        game.Spawner.Register("pickup", InventoryJsonContext.Default.Pickup);

        InventorySystem inventories = new(game, settings ?? new InventorySettings());
        game.Services.AddService(inventories);
        PickupSystem pickups = new(game, inventories);
        game.Services.AddService(pickups);
        return inventories;
    }
}
