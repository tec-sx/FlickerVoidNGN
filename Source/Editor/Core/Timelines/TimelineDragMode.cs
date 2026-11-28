namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What a drag on a timeline does as the pointer moves.</summary>
public enum TimelineDragMode
{
    None,
    Scrub,
    RulerPress,
    CreateRegion,
    MoveItem,
    ResizeItemStart,
    ResizeItemEnd,
    MoveRegion,
    ResizeRegionStart,
    ResizeRegionEnd,
}
