namespace FlickerVoid.Engine.Attributes.Values;

/// <summary>One attribute while the game plays: its definition, when it has one, its base value and its current value with modifiers.</summary>
public sealed class AttributeValue(AttributeDefinition? definition, float baseValue)
{
    public AttributeDefinition? Definition { get; } = definition;

    public float Base { get; set; } = baseValue;

    public float Current { get; set; } = baseValue;

    public float Clamp(float value)
    {
        if (Definition is null)
        {
            return value;
        }

        return Definition.Clamp(value);
    }
}
