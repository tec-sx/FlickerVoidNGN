using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A lighting mood file and the conditions under which the story wants it.</summary>
public sealed class MoodRule
{
    public ConditionSet When { get; init; } = new();

    /// <summary>The mood file's path in the content folder.</summary>
    public string Mood { get; init; } = string.Empty;
}
