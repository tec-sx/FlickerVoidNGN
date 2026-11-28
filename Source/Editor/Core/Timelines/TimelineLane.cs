using System.Collections.ObjectModel;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>A row of a timeline: its header (name, icon, colour) and its items, with lanes under it (a layer's fills) drawn indented.</summary>
public sealed class TimelineLane
{
    /// <summary>A layer's lane height.</summary>
    public const double DefaultHeight = 84;

    /// <summary>A sub-lane's height, as a fills lane.</summary>
    public const double SubHeight = 30;

    public object? Source { get; init; }

    public string Name { get; set; } = "";

    /// <summary>The name of the header's icon, or null.</summary>
    public string? Icon { get; set; }

    /// <summary>The theme colour's key ("EditorTrack1"), used for the header's edge and the lane's items.</summary>
    public string Tint { get; set; } = "EditorTrack1";

    public double Height { get; set; } = DefaultHeight;

    /// <summary>A short note in the header after the name, or null.</summary>
    public string? Detail { get; set; }

    public bool IsSelected { get; set; }

    /// <summary>Drawn on the raised ground of a lane under a layer or outside the loop (fills, shots); lanes under others always are.</summary>
    public bool IsRaised { get; set; }

    /// <summary>Drawn on a darker ground, as a layer held out of the mix.</summary>
    public bool IsHeld { get; set; }

    public List<TimelineItem> Items { get; } = [];

    public ObservableCollection<TimelineLane> Children { get; } = [];
}
