using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>
/// Builds a timeline of 16 bars in 4/4 on 1024 pixels (64 a bar, 16 a beat): a Bass lane 84 high with a resizable clip over bars
/// 2 to 6 (pixels 128 to 384) and a key at bar 8 (pixel 512), a fills lane 30 high under it with a fill over bar 5, and regions A
/// over bars 0 to 4 and B over bars 8 to 12.
/// </summary>
internal sealed class TimelineEditTestsBuilder
{
    private ITimeScale _scale = new BarScale(beatsPerBar: 4, phraseBars: 4);
    private bool _createsRegions;

    public RecordingTimelineEditor Editor { get; } = new();

    public TimelineItem Clip { get; } = new() { Start = 2, Length = 4, CanResize = true };

    public TimelineItem Key { get; } = new() { Kind = TimelineKinds.Key, Start = 8 };

    public TimelineItem Fill { get; } = new() { Kind = TimelineKinds.Fill, Start = 5, Length = 1 };

    public TimelineRegion A { get; } = new() { Name = "A", Start = 0, Length = 4 };

    public TimelineRegion B { get; } = new() { Name = "B", Start = 8, Length = 4 };

    public TimelineEditTestsBuilder InHours()
    {
        _scale = new HourScale();
        return this;
    }

    public TimelineEditTestsBuilder CreatingRegions()
    {
        _createsRegions = true;
        return this;
    }

    public TimelineViewModel Build()
    {
        TimeRange range = new();
        range.Resize(1024);
        range.Show(0, 16);
        TimelineViewModel timeline = new(_scale, range) { Editor = Editor, CanCreateRegions = _createsRegions };
        TimelineLane bass = new() { Name = "Bass" };
        bass.Items.Add(Clip);
        bass.Items.Add(Key);
        TimelineLane fills = new() { Name = "fills", Height = TimelineLane.SubHeight };
        fills.Items.Add(Fill);
        bass.Children.Add(fills);
        timeline.Lanes.Add(bass);
        timeline.Regions.Add(A);
        timeline.Regions.Add(B);
        return timeline;
    }
}
