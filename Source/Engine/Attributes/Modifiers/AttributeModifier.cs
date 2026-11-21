using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Modifiers;

/// <summary>One change a modifier makes to an attribute's current value.</summary>
public sealed class AttributeModifier
{
    public Tag Attribute { get; set; }

    public ModifierOperation Operation { get; set; }

    public float Value { get; set; }
}
