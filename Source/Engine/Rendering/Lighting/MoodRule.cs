using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>A lighting mood file and the conditions under which the story wants it.</summary>
public sealed class MoodRule
{
    public ConditionSet When { get; init; } = new();

    /// <summary>The mood file's path in the content folder.</summary>
    [ContentFile(".fvmood")]
    public string Mood { get; init; } = string.Empty;
}

