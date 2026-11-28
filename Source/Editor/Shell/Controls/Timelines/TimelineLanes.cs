using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The lanes of every timeline (rule R28): one row for each lane and the lanes under it, its grid from the scale, and the items
/// within the shown span, each drawn by the painter of its kind; nothing off screen costs anything. As tall as its rows, so a
/// scroller around it and the lane headers scrolls both together. A press picks an item (Ctrl adds it), a drag moves or resizes
/// it, a press on empty lane scrubs; the arrows nudge, Delete removes, Home and End move the cursor; the wheel zooms the time
/// (<see cref="TimelineInput.Read"/>), Shift with it pans, and Alt turns snapping off.
/// </summary>
public sealed class TimelineLanes : TimelineControl
{
    private readonly List<TimeTick> _ticks = [];
    private TimelineDrag? _drag;

    static TimelineLanes()
    {
        FocusableProperty.OverrideDefaultValue<TimelineLanes>(true);
    }

    public TimelineLanes()
    {
        ClipToBounds = true;
    }

    protected override bool NeedsPulse(TimelineViewModel timeline)
    {
        foreach (TimelineRow row in timeline.GetRows())
        {
            foreach (TimelineItem item in row.Lane.Items)
            {
                if (item.IsQueued)
                {
                    return true;
                }
            }
        }

        return false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = Timeline?.ContentHeight ?? 0;
        return new Size(0, height);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        StyledColors colors = StyledColors.Read(this);
        context.DrawRectangle(new SolidColorBrush(colors.Deep), null, bounds);
        if (Timeline is not { } timeline)
        {
            return;
        }

        timeline.Scale.GetTicks(timeline.Range, _ticks);
        double pulse = Pulse;
        foreach (TimelineRow row in timeline.GetRows())
        {
            Rect area = new(0, row.Top, bounds.Width, row.Lane.Height);
            DrawRow(context, timeline, row, area, colors, pulse);
        }

        TimelineDrawing.ShadeOutsidePeriod(context, timeline, bounds, ThemeColors.Fade(colors.Base, 0.6));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Timeline is not { } timeline || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point position = e.GetPosition(this);
        TimelineHit hit = TimelineEdit.HitLanes(timeline, position.X, position.Y);
        bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool add = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        _drag = TimelineEdit.Press(timeline, hit, position.X, snap, add);
        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_drag is null)
        {
            UpdateCursor(position);
            return;
        }

        bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _drag.Move(position.X, snap);
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
        if (Timeline is { } timeline)
        {
            TimelineInput.Wheel(timeline, e, e.GetPosition(this).X);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Timeline is { } timeline)
        {
            TimelineInput.Keys(timeline, e);
        }
    }

    private void DrawRow(DrawingContext context, TimelineViewModel timeline, TimelineRow row, Rect area, StyledColors colors, double pulse)
    {
        if (row.Lane.IsRaised || row.Depth > 0)
        {
            Color raised = ThemeColors.Mix(colors.Base, colors.Deep, 0.55);
            context.DrawRectangle(new SolidColorBrush(raised), null, area);
        }
        else if (row.Lane.IsHeld)
        {
            Color held = ThemeColors.Mix(colors.Base, colors.Deep, 0.5);
            context.DrawRectangle(new SolidColorBrush(held), null, area);
        }

        using DrawingContext.PushedState clip = context.PushClip(area);
        TimelineDrawing.Grid(context, _ticks, timeline.Range, area, colors.Line);
        Color tint = ThemeColors.Find(this, row.Lane.Tint);
        LanePaint paint = new()
        {
            Tint = tint,
            Panel = colors.Panel,
            Text = colors.Text,
            MutedText = colors.MutedText,
            Hint = colors.Hint,
            Ink = colors.Ink,
            Pulse = pulse,
            PixelsPerUnit = timeline.Range.PixelsPerUnit,
        };
        foreach (TimelineItem item in row.Lane.Items)
        {
            if (item.End < timeline.Range.MinTime || item.Start > timeline.Range.MaxTime)
            {
                continue;
            }

            double left = Math.Round(timeline.Range.ToX(item.Start));
            double right = Math.Round(timeline.Range.ToX(item.End));
            Rect span = new(left, area.Top, Math.Max(right - left, 0), area.Height);
            ILaneItemPainter painter = TimelinePainters.Find(item.Kind);
            painter.Paint(context, item, span, paint);
        }

        TimelineDrawing.HorizontalLine(context, colors.Line, area.Bottom - 1, area.Left, area.Right);
    }

    /// <summary>Shows a resize arrow over the edges of what can be resized, and the hand over what can be moved.</summary>
    private void UpdateCursor(Point position)
    {
        if (Timeline is not { } timeline)
        {
            return;
        }

        TimelineHit hit = TimelineEdit.HitLanes(timeline, position.X, position.Y);
        StandardCursorType type = hit.Kind switch
        {
            TimelineHitKind.ItemStart or TimelineHitKind.ItemEnd => StandardCursorType.SizeWestEast,
            TimelineHitKind.Item when hit.Item is { CanMove: true } => StandardCursorType.Hand,
            _ => StandardCursorType.Arrow,
        };
        Cursor = new Cursor(type);
    }
}
