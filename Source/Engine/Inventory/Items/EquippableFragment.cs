using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Inventory.Items;

/// <summary>Worn in one of its slots; the tags it grants (such as Disguise.Faction.Police) are the wearer's while it is worn.</summary>
public sealed class EquippableFragment : ItemFragment
{
    /// <summary>Worn in any one of these, such as either arm for a bracelet.</summary>
    public List<Tag> Slots { get; set; } = [];

    /// <summary>Covered while worn: whatever is worn there comes off and they stay empty.</summary>
    public List<Tag> BlockedSlots { get; set; } = [];

    public List<Tag> GrantedTags { get; set; } = [];

    /// <summary>Story gating, such as an outfit that can't be worn before a quest step.</summary>
    public ConditionSet EquipConditions { get; set; } = new();

    /// <summary>The model the game shows while it is worn.</summary>
    [ContentFile(".glb")]
    public string Mesh { get; set; } = string.Empty;
}
