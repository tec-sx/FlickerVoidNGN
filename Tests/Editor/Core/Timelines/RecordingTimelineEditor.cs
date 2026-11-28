using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>An owner of a timeline that records the edits asked of it.</summary>
internal sealed class RecordingTimelineEditor : ITimelineEditor
{
    public List<TimelineChange> Changes { get; } = [];

    public int Completions { get; private set; }

    public TimelineChange Last => Changes[^1];

    public void Apply(TimelineChange change) => Changes.Add(change);

    public void Complete() => Completions++;
}
