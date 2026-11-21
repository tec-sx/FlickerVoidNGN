using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>An attribute set while the game plays: the values by attribute, the modifiers applied and the tags they added to the entity.</summary>
public struct ActiveAttributeSet
{
    public Dictionary<Tag, AttributeValue> Values { get; set; }

    public List<AppliedModifier> Modifiers { get; set; }

    /// <summary>The modifiers' granted tags added to the entity's <see cref="OwnedTags"/>, so only they are taken off again.</summary>
    public List<Tag> AppliedTags { get; set; }
}
