using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>The carry limits every inventory with limits follows, and the item categories kept apart as keys.</summary>
public sealed class InventorySettings
{
    /// <summary>Carried weight is limited.</summary>
    public bool EnforceWeight { get; set; } = true;

    /// <summary>Items take up cells.</summary>
    public bool EnforceSpace { get; set; } = true;

    /// <summary>Kilograms a character carries with nothing equipped.</summary>
    public float BaseWeightLimit { get; set; } = 20;

    /// <summary>Cells with no bag or backpack: pockets.</summary>
    public int BasePocketCells { get; set; } = 6;

    /// <summary>Item categories listed under Keys and Quest; they take no weight and no cells.</summary>
    public List<Tag> KeyItemCategories { get; set; } = [];

    public bool IsKeyItem(ItemDefinition item)
    {
        foreach (Tag category in KeyItemCategories)
        {
            if (item.Category.MatchesTag(category))
            {
                return true;
            }
        }

        return false;
    }
}
