namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>What a turn of the wheel does over a timeline's lanes or ruler (rule R28), from the keys held with it.</summary>
public enum TimelineWheel
{
    /// <summary>Alone: zooms the time about the pointer.</summary>
    ZoomTime,

    /// <summary>With Ctrl: makes the lanes taller or shorter.</summary>
    ScaleLanes,

    /// <summary>With Shift, or turned sideways: moves along the time.</summary>
    Pan,

    /// <summary>With Alt: left to the scroller around the lanes, which scrolls them up and down.</summary>
    Scroll,
}
