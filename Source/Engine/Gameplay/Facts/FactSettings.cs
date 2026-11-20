using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>The declared facts: defaults, ranges and value labels.</summary>
public sealed class FactSettings
{
    public List<FactDefinition> Definitions { get; init; } = [];

    public FactDefinition? FindDefinition(Tag tag) => Definitions.Find(definition => definition.Tag == tag);

    /// <summary>Label of the value for enum-like facts, or null.</summary>
    public string? GetValueName(Tag tag, int value)
    {
        FactDefinition? definition = FindDefinition(tag);
        if (definition is null || value < 0 || value >= definition.ValueNames.Count)
        {
            return null;
        }

        return definition.ValueNames[value];
    }
}
