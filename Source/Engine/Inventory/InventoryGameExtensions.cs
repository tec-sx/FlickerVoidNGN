using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Inventory;

/// <summary>Adds inventories to a game: the <c>item</c> data asset type and its fragments, the <c>inventory</c> component and the system that holds them.</summary>
public static class InventoryGameExtensions
{
    public static InventorySystem AddInventory(this Game game, InventorySettings? settings = null)
    {
        game.Data.AddResolver(InventoryJsonContext.Default);
        game.Data.Register<ItemDefinition>("item");
        game.Data.RegisterDerived<ItemFragment, UsableFragment>("usable");
        game.Data.RegisterDerived<ItemFragment, ChargesFragment>("charges");
        game.Data.RegisterDerived<ItemFragment, ValueFragment>("value");
        game.Spawner.Register("inventory", InventoryJsonContext.Default.Inventory);

        InventorySystem inventories = new(game, settings ?? new InventorySettings());
        game.Services.AddService(inventories);
        return inventories;
    }
}
