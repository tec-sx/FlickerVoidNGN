namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>A named, coloured span over every lane of a timeline (a cue's loop), with a tab on the ruler and a state.</summary>
public sealed class TimelineRegion
{
    public object? Source { get; init; }

    public double Start { get; set; }

    public double Length { get; set; }

    public double End => Start + Length;

    public string Name { get; set; } = "";

    /// <summary>The theme colour's key ("EditorCue1").</summary>
    public string Tint { get; set; } = "EditorCue1";

    public RegionState State { get; set; }

    /// <summary>The key that triggers it, shown in a box on its tab, or null.</summary>
    public string? Key { get; set; }

    /// <summary>A note at the tab's right end ("playing", "starts in 3 bars"), or null.</summary>
    public string? Note { get; set; }

    public bool IsSelected { get; set; }
}
