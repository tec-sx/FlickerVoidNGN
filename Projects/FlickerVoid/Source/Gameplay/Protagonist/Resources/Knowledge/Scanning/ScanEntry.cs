using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>One line a scan reveals, shown only while its conditions pass (a Perception check, a clue known).</summary>
public sealed class ScanEntry
{
    public string Label { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public ConditionSet VisibleWhen { get; set; } = new();
}
