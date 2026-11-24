using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Loot;
using FlickerVoid.Engine.Inventory.Outfits;
using FlickerVoid.Engine.Inventory.Pickups;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Inventory.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Inventory;

/// <summary>Adds inventories to a game: the <c>item</c> and <c>lootTable</c> data asset types and the item fragments, the inventory conditions and effects, the <c>inventory</c>, <c>pickup</c>, <c>equipment</c> and <c>lootable</c> components, the systems that run them, and what saves keep of them.</summary>
public static class InventoryGameExtensions
{
    public static InventorySystem AddInventory(this Game game, InventorySettings? settings = null, Random? random = null)
    {
        game.Data.AddResolver(InventoryJsonContext.Default);
        game.Data.Register<ItemDefinition>("item");
        game.Data.RegisterDerived<ItemFragment, UsableFragment>("usable");
        game.Data.RegisterDerived<ItemFragment, ChargesFragment>("charges");
        game.Data.RegisterDerived<ItemFragment, ValueFragment>("value");
        game.Data.RegisterDerived<ItemFragment, EquippableFragment>("equippable");
        game.Data.RegisterDerived<ItemFragment, ContainerFragment>("container");
        game.Data.RegisterDerived<Condition, IsWearingCondition>("isWearing");
        game.Data.Register<LootTableDefinition>("lootTable");
        game.Data.RegisterDerived<Effect, GiveLootEffect>("giveLoot");
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
        game.Spawner.Register("equipment", InventoryJsonContext.Default.Equipment);
        game.Spawner.Register("lootable", InventoryJsonContext.Default.Lootable);
        game.Data.RegisterDerived<ItemInstanceData, Stolen>("stolen");
        game.Data.RegisterDerived<ItemInstanceData, ChargesLeft>("chargesLeft");
        game.SaveStates.AddComponent("lootable", InventoryJsonContext.Default.Lootable);

        InventorySystem inventories = new(game, settings ?? new InventorySettings());
        game.Services.AddService(inventories);
        PickupSystem pickups = new(game, inventories);
        game.Services.AddService(pickups);
        EquipmentSystem equipment = new(game, inventories);
        game.Services.AddService(equipment);
        LootSystem loot = new(game, inventories, random ?? game.Random);
        game.Services.AddService(loot);
        return inventories;
    }
}
