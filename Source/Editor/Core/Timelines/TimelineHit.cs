namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What is under the pointer on a timeline: the kind of hit, the lane's row, the item or the region.</summary>
public readonly record struct TimelineHit(TimelineHitKind Kind, TimelineRow? Row, TimelineItem? Item, TimelineRegion? Region)
{
    public static readonly TimelineHit None = new(TimelineHitKind.None, null, null, null);
}
