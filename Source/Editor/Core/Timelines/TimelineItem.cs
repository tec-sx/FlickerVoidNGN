namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// Something on a lane of a timeline: a span (a clip, a fill) or a point when its length is 0 (a key, a marker). Its kind names
/// the painter that draws it; its source is what the owning editor knows it by.
/// </summary>
public sealed class TimelineItem
{
    public object? Source { get; init; }

    /// <summary>The painter's name: "clip", "fill", "key", "marker" and so on.</summary>
    public string Kind { get; init; } = TimelineKinds.Clip;

    public double Start { get; set; }

    /// <summary>The length in the scale's unit; 0 makes a point.</summary>
    public double Length { get; set; }

    public double End => Start + Length;

    public bool IsPoint => Length <= 0;

    public string? Label { get; set; }

    /// <summary>The name of an icon drawn before the label, or null.</summary>
    public string? Icon { get; set; }

    public bool IsSelected { get; set; }

    /// <summary>Drawn faded, as a layer that does not play now.</summary>
    public bool IsDimmed { get; set; }

    /// <summary>Waiting for its beat to start: drawn pulsing.</summary>
    public bool IsQueued { get; set; }

    /// <summary>A repeated pass of the clip before it, drawn lighter.</summary>
    public bool IsRepeat { get; set; }

    public bool CanMove { get; init; } = true;

    public bool CanResize { get; init; }

    /// <summary>How much of it has played, from 0 to 1, or NaN when it is not playing.</summary>
    public double Progress { get; set; } = double.NaN;

    /// <summary>What a painter draws inside it (a waveform's peaks), or null.</summary>
    public object? Look { get; set; }
}
