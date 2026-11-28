namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What is under the pointer on a timeline.</summary>
public enum TimelineHitKind
{
    None,
    Lane,
    Item,
    ItemStart,
    ItemEnd,
    Ruler,
    RegionTab,
    RegionStart,
    RegionEnd,
}
