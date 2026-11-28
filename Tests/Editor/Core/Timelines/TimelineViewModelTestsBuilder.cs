using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

/// <summary>
/// Builds a timeline in seconds on 200 pixels with two lanes: Bass (84 high, a clip from 1 to 3 s, with a fills lane 30 high
/// under it holding a fill from 3 to 4 s) and Keys (40 high, a key at 6 s).
/// </summary>
internal sealed class TimelineViewModelTestsBuilder
{
    private ITimeScale _scale = new SecondScale();

    public TimelineItem Clip { get; } = new() { Start = 1, Length = 2 };

    public TimelineItem Fill { get; } = new() { Kind = TimelineKinds.Fill, Start = 3, Length = 1 };

    public TimelineItem Key { get; } = new() { Kind = TimelineKinds.Key, Start = 6 };

    public TimelineViewModelTestsBuilder InHours()
    {
        _scale = new HourScale();
        return this;
    }

    public TimelineViewModel Build()
    {
        TimelineViewModel timeline = new(_scale);
        timeline.Range.Resize(200);
        TimelineLane bass = new() { Name = "Bass" };
        bass.Items.Add(Clip);
        TimelineLane fills = new() { Name = "fills", Height = TimelineLane.SubHeight };
        fills.Items.Add(Fill);
        bass.Children.Add(fills);
        TimelineLane keys = new() { Name = "Keys", Height = 40 };
        keys.Items.Add(Key);
        timeline.Lanes.Add(bass);
        timeline.Lanes.Add(keys);
        return timeline;
    }
}
