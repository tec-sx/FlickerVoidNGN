namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>An edit a timeline asks its owner for: the item or region, and its new start and length (snapped and clamped).</summary>
public readonly record struct TimelineChange(
    TimelineChangeKind Kind,
    TimelineItem? Item,
    TimelineRegion? Region,
    double Start,
    double Length);
