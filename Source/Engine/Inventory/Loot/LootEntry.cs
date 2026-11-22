using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>One line of a loot table: an item or a nested table, with a quantity range, a chance and conditions.</summary>
public sealed class LootEntry
{
    /// <summary>The <see cref="Items.ItemDefinition"/> data asset given.</summary>
    [ContentFile(".fvdata")]
    public string Item { get; set; } = string.Empty;

    /// <summary>A <see cref="LootTableDefinition"/> rolled instead of giving an item; nest tables to go past three entries.</summary>
    [ContentFile(".fvdata")]
    public string Table { get; set; } = string.Empty;

    /// <summary>Fewest units given, or times the nested table is rolled.</summary>
    public int MinQuantity { get; set; } = 1;

    /// <summary>Most units given, or times the nested table is rolled.</summary>
    public int MaxQuantity { get; set; } = 1;

    /// <summary>Probability the entry is given, from 0 to 1.</summary>
    public float Chance { get; set; } = 1;

    /// <summary>The instigator is who loots, the target what is looted or what receives a give loot effect.</summary>
    public ConditionSet Conditions { get; set; } = new();
}
