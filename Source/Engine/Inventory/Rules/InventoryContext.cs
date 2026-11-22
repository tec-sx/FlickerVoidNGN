using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Rules;

/// <summary>What the inventory conditions and effects find in their context: the inventory system, the entity they look at and the items they name.</summary>
internal static class InventoryContext
{
    public static bool TryGetOwner(ConditionContext context, ContextEntity which, out InventorySystem inventories, out Entity owner)
    {
        IEntityView? view = context.GetEntity(which);
        InventorySystem? system = context.GetService<InventorySystem>();
        if (system is null || view is not WorldEntityView worldView)
        {
            inventories = null!;
            owner = Entity.Null;
            return false;
        }

        inventories = system;
        owner = worldView.Entity;
        return true;
    }

    /// <summary>The item data asset at <paramref name="path"/>; null when there are no data assets or it can't be read.</summary>
    public static ItemDefinition? LoadItem(ConditionContext context, string path)
    {
        DataAssets? data = context.GetService<DataAssets>();
        if (data is null || path.Length == 0)
        {
            return null;
        }

        try
        {
            return data.Load<ItemDefinition>(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    /// <summary>The file name of an item path without its extension, for descriptions.</summary>
    public static string Name(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.Length == 0)
        {
            return "<none>";
        }

        return name;
    }
}
