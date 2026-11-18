using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Scales;

/// <summary>A named range of a scale, from its threshold up to the next band's.</summary>
public sealed class ScaleBand
{
    /// <summary>Value at or above which this band applies, up to the next band's threshold.</summary>
    public int Threshold { get; init; }

    /// <summary>Identifies the band for conditions and game rules.</summary>
    public Tag Tag { get; init; }

    public string Label { get; init; } = string.Empty;
}
