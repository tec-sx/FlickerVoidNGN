using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The time ruler of every timeline (rule R28): its scale's ticks and labels (section starts bright), the shade outside a day,
/// the regions' tabs, and the position line with its chip. Pressing scrubs the cursor, a drag along empty ruler draws a region
/// where the timeline allows it, a drag on a tab moves its region and on its edges resizes it; the wheel zooms about the pointer
/// and Shift with the wheel pans. Holding Alt turns snapping off.
/// </summary>
public sealed class TimeRuler : TimelineControl
{
    public const double DefaultHeight = 24;

    /// <summary>Where a label and its tick start from the ruler's top.</summary>
    public const double LabelTop = 5;

    /// <summary>Where the position chip sits from the ruler's top.</summary>
    public const double ChipTop = 4;

    public static readonly StyledProperty<bool> ShowsRegionsProperty = AvaloniaProperty.Register<TimeRuler, bool>(nameof(ShowsRegions), true);

    public static readonly StyledProperty<bool> ShowsPositionProperty = AvaloniaProperty.Register<TimeRuler, bool>(nameof(ShowsPosition), true);

    private readonly List<TimeTick> _ticks = [];
    private TimelineDrag? _drag;

    static TimeRuler()
    {
        AffectsRender<TimeRuler>(ShowsRegionsProperty, ShowsPositionProperty);
        FocusableProperty.OverrideDefaultValue<TimeRuler>(true);
    }

    public TimeRuler()
    {
        Height = DefaultHeight;
        ClipToBounds = true;
    }

    public bool ShowsRegions
    {
        get => GetValue(ShowsRegionsProperty);
        set => SetValue(ShowsRegionsProperty, value);
    }

    public bool ShowsPosition
    {
        get => GetValue(ShowsPositionProperty);
        set => SetValue(ShowsPositionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        StyledColors colors = StyledColors.Read(this);
        context.DrawRectangle(new SolidColorBrush(colors.Base), null, bounds);
        if (Timeline is not { } timeline)
        {
            return;
        }

        TimelineDrawing.ShadeOutsidePeriod(context, timeline, bounds, ThemeColors.Fade(colors.Deep, 0.6));
        DrawTicks(context, timeline, bounds, colors);
        if (ShowsRegions)
        {
            DrawTabs(context, timeline, bounds, colors);
        }

        TimelineDrawing.HorizontalLine(context, colors.Line, bounds.Height - 1, 0, bounds.Width);
        if (!ShowsPosition)
        {
            return;
        }

        double time = TimelineDrawing.PositionOf(timeline);
        double x = timeline.Range.ToX(time);
        string label = timeline.Scale.Format(time);
        TimelineDrawing.Position(context, x, bounds.Height, ChipTop, label, colors.Accent, colors.AccentInk);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Timeline is not { } timeline || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        double x = e.GetPosition(this).X;
        TimelineHit hit = TimelineEdit.HitRuler(timeline, x);
        if (!ShowsRegions)
        {
            hit = new TimelineHit(TimelineHitKind.Ruler, null, null, null);
        }

        bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _drag = TimelineEdit.Press(timeline, hit, x, snap, add: false);
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is null)
        {
            return;
        }

        bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _drag.Move(e.GetPosition(this).X, snap);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _drag?.Release();
        _drag = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Timeline is not { } timeline)
        {
            return;
        }

        TimelineInput.Wheel(timeline, e, e.GetPosition(this).X);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Timeline is { } timeline)
        {
            TimelineInput.Keys(timeline, e);
        }
    }

    private void DrawTicks(DrawingContext context, TimelineViewModel timeline, Rect bounds, StyledColors colors)
    {
        timeline.Scale.GetTicks(timeline.Range, _ticks);
        foreach (TimeTick tick in _ticks)
        {
            double x = timeline.Range.ToX(tick.Time);
            if (tick.Label is null)
            {
                double length = 6;
                if (tick.Level == TickLevel.Minor)
                {
                    length = 4;
                }

                TimelineDrawing.VerticalLine(context, colors.Line, x, bounds.Height - length, bounds.Height);
                continue;
            }

            bool section = tick.Level == TickLevel.Section;
            Color line = colors.Line;
            Color text = colors.Hint;
            FontWeight weight = FontWeight.Normal;
            if (section)
            {
                line = colors.Hint;
                text = colors.Text;
                weight = FontWeight.SemiBold;
            }

            TimelineDrawing.VerticalLine(context, line, x, LabelTop, bounds.Height);
            FormattedText label = TimelineDrawing.Text(tick.Label, 10.5, text, weight);
            context.DrawText(label, new Point(TimelineDrawing.Crisp(x) + 4, LabelTop - 1));
        }
    }

    private void DrawTabs(DrawingContext context, TimelineViewModel timeline, Rect bounds, StyledColors colors)
    {
        foreach (TimelineRegion region in timeline.Regions)
        {
            Rect tab = GetSpan(timeline, region.Start, region.End, bounds);
            if (tab.Width <= 0)
            {
                continue;
            }

            Color tint = ThemeColors.Find(this, region.Tint);
            StyledColors tinted = colors.WithTint(tint);
            RegionDrawing.DrawTab(context, region, tab, timeline.Scale.Unit, tinted);
        }

        if (timeline.Draft is { } draft)
        {
            Rect span = GetSpan(timeline, draft.Start, draft.End, bounds);
            RegionDrawing.DrawDraft(context, span, colors.Accent);
        }
    }

    private static Rect GetSpan(TimelineViewModel timeline, double start, double end, Rect bounds)
    {
        double left = Math.Round(timeline.Range.ToX(start));
        double right = Math.Round(timeline.Range.ToX(end));
        return new Rect(left, 0, Math.Max(right - left, 0), bounds.Height);
    }
}
