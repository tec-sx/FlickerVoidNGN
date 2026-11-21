using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Inventory.Outfits;

/// <summary>What an entity wears while the game plays, one item per slot, and the tags it put among the entity's owned tags.</summary>
public struct ActiveEquipment
{
    public List<EquippedItem> Items { get; set; }

    public bool RequireInventory { get; set; }

    /// <summary>The granted tags added to the entity's <see cref="OwnedTags"/>, so only they are taken off again.</summary>
    public List<Tag> AppliedTags { get; set; }

    public readonly bool IsWorn(Guid stack)
    {
        if (stack == Guid.Empty)
        {
            return false;
        }

        return Items.Exists(worn => worn.Stack == stack);
    }
}
