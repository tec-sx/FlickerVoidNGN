using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>An attribute an entity starts with and its base value.</summary>
public sealed class AttributeEntry
{
    public Tag Attribute { get; set; }

    public float Value { get; set; }
}
