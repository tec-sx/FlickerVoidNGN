namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>The editor that owns a timeline's data: it applies the timeline's edits to its own model, through undo.</summary>
public interface ITimelineEditor
{
    /// <summary>Applies an edit; a drag asks again on each move, with the start and length from where it began.</summary>
    void Apply(TimelineChange change);

    /// <summary>Ends a drag, so the edits since it began make one undo step.</summary>
    void Complete();
}
