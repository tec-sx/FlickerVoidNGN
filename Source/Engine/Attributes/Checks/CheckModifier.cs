using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>A bonus to a check that applies when its conditions pass, the way interaction offers gate themselves.</summary>
public sealed class CheckModifier
{
    public LocalizedText Label { get; set; } = new();

    public ConditionSet Conditions { get; set; } = new();

    public int Bonus { get; set; }
}
