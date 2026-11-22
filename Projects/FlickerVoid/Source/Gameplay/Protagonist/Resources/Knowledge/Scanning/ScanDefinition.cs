using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

/// <summary>What a scannable reveals: its display is the scan's title, and its effects apply once, on the first completed scan.</summary>
public sealed class ScanDefinition : Definition
{
    public Tag Category { get; set; }

    /// <summary>Seconds of holding scan; 0 scans at once.</summary>
    public float ScanDuration { get; set; } = 1;

    /// <summary>Checked with the scanner as instigator and the scannable as target.</summary>
    public ConditionSet ScannableWhen { get; set; } = new();

    public List<ScanEntry> Entries { get; set; } = [];

    public EffectList OnScanned { get; set; } = new();
}
