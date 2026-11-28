using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

using FlickerVoid.Editor.Controls.Timelines;
using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Tests.Controls.Timelines;

/// <summary>
/// Shows a timeline of 16 bars over 640 px (40 a bar) in a headless window: a ruler 24 high over the lanes (a Bass lane 84 high
/// with a clip over bars 2 to 6, a fills lane 30 high under it) and an overlay over both, with an owner that records edits.
/// </summary>
internal sealed class TimelineControlsTestsBuilder
{
    private Window? _window;

    public RecordingEditor Editor { get; } = new();

    public TimelineViewModel Timeline { get; private set; } = null!;

    public TimelineItem Clip { get; } = new() { Start = 2, Length = 4, CanResize = true, Label = "BassLoop" };

    public TimeRuler Ruler { get; private set; } = null!;

    public TimelineLanes Lanes { get; private set; } = null!;

    public TimelineOverlay Overlay { get; private set; } = null!;

    public async Task Run(Action test)
    {
        await HeadlessEditor.Session.Dispatch(
            () =>
            {
                Show();
                test();
                _window!.Close();
            },
            CancellationToken.None);
    }

    public static void Settle() => Dispatcher.UIThread.RunJobs();

    /// <summary>Presses, drags and releases the left button between two points of the control.</summary>
    public void Drag(Control control, Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point start = control.TranslatePoint(from, _window!)!.Value;
        Point end = control.TranslatePoint(to, _window!)!.Value;
        _window!.MouseDown(start, MouseButton.Left, modifiers);
        _window!.MouseMove(end, modifiers | RawInputModifiers.LeftMouseButton);
        _window!.MouseUp(end, MouseButton.Left, modifiers);
        Settle();
    }

    public void Click(Control control, Point at, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point point = control.TranslatePoint(at, _window!)!.Value;
        _window!.MouseDown(point, MouseButton.Left, modifiers);
        _window!.MouseUp(point, MouseButton.Left, modifiers);
        Settle();
    }

    public void Wheel(Control control, Point at, double notches, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Point point = control.TranslatePoint(at, _window!)!.Value;
        _window!.MouseWheel(point, new Vector(0, notches), modifiers);
        Settle();
    }

    private void Show()
    {
        TimeRange range = new() { Earliest = 0 };
        Timeline = new TimelineViewModel(new BarScale(4, 4), range) { Editor = Editor, CanCreateRegions = true };
        TimelineLane bass = new() { Name = "Bass" };
        bass.Items.Add(Clip);
        bass.Children.Add(new TimelineLane { Name = "fills", Height = TimelineLane.SubHeight });
        Timeline.Lanes.Add(bass);
        Ruler = new TimeRuler { Timeline = Timeline };
        Lanes = new TimelineLanes { Timeline = Timeline, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        Overlay = new TimelineOverlay { Timeline = Timeline };
        Grid area = new() { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(Lanes, 1);
        Grid.SetRowSpan(Overlay, 2);
        area.Children.Add(Ruler);
        area.Children.Add(Lanes);
        area.Children.Add(Overlay);
        _window = new Window { Content = area, Width = 640, Height = 300 };
        _window.Show();
        Settle();
        Timeline.Range.Show(0, 16);
        Settle();
    }

    /// <summary>An owner of the timeline that records the edits asked of it.</summary>
    internal sealed class RecordingEditor : ITimelineEditor
    {
        public List<TimelineChange> Changes { get; } = [];

        public int Completions { get; private set; }

        public void Apply(TimelineChange change) => Changes.Add(change);

        public void Complete() => Completions++;
    }
}
