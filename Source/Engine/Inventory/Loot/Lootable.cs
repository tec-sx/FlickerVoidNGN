using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>Fills the entity's inventory from a loot table once, such as when a container is first opened.</summary>
public struct Lootable
{
    public Lootable()
    {
    }

    /// <summary>The <see cref="LootTableDefinition"/> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Table { get; set; } = string.Empty;

    /// <summary>The faction or character id owning the contents, so what is taken is stolen; none leaves it free to take.</summary>
    public Tag Owner { get; set; }

    /// <summary>The table was rolled; it is saved so a container fills only once.</summary>
    public bool Rolled { get; set; }
}
