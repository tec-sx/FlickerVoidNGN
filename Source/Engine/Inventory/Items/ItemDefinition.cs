using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>An item that can lie in the world and be carried; its behaviour is composed through fragments.</summary>
public sealed class ItemDefinition : Definition
{
    public Tag Category { get; set; }

    /// <summary>Kilograms of one unit.</summary>
    public float Weight { get; set; } = 0.1f;

    /// <summary>Cells across; each stack claims its footprint once however many units it holds.</summary>
    public int Width { get; set; } = 1;

    /// <summary>Cells down.</summary>
    public int Height { get; set; } = 1;

    /// <summary>Units one stack holds; more start new stacks. 1 makes every unit its own entry, 0 is unlimited.</summary>
    public int MaxQuantity { get; set; }

    /// <summary>Quest items can't be dropped or given away by the player.</summary>
    public bool QuestItem { get; set; }

    public List<ItemFragment> Fragments { get; set; } = [];

    [JsonIgnore]
    public int CellCount => Math.Max(1, Width) * Math.Max(1, Height);

    [JsonIgnore]
    public bool IsUsable => FindFragment<UsableFragment>() is not null;

    public T? FindFragment<T>()
        where T : ItemFragment
    {
        return Fragments.OfType<T>().FirstOrDefault();
    }
}
