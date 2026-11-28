namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>A lane laid out on a timeline: its top in pixels and how deep it is under other lanes.</summary>
public readonly record struct TimelineRow(TimelineLane Lane, double Top, int Depth)
{
    public double Bottom => Top + Lane.Height;
}
