namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>What an edit on a timeline does.</summary>
public enum TimelineChangeKind
{
    MoveItem,
    ResizeItem,
    DeleteItem,
    MoveRegion,
    ResizeRegion,
    CreateRegion,
    DeleteRegion,
}
