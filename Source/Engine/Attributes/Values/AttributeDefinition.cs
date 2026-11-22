using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>Display and range data for one attribute (Perception, Stealth, Health), named by its id; entities hold its value in their <see cref="AttributeSet"/>.</summary>
public sealed class AttributeDefinition : Definition
{
    /// <summary>Groups attributes in the interface and debug output, such as <c>Attribute.Skill</c>.</summary>
    public Tag Category { get; set; }

    public float Min { get; set; }

    public float Max { get; set; } = 100;

    /// <summary>Rounds the value, for attributes used as whole numbers.</summary>
    public bool Integer { get; set; }

    /// <summary>Clamps to Min and Max and rounds when Integer is set.</summary>
    public float Clamp(float value)
    {
        float clamped = Math.Clamp(value, Min, Math.Max(Min, Max));
        if (Integer)
        {
            return MathF.Round(clamped, MidpointRounding.AwayFromZero);
        }

        return clamped;
    }
}
