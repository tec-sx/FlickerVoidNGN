using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>Items and money a container, body or reward gives; keep three entries at most and nest tables for more.</summary>
public sealed class LootTableDefinition : Definition
{
    public List<LootEntry> Entries { get; set; } = [];

    public int MinMoney { get; set; }

    public int MaxMoney { get; set; }
}
