using System.Collections.Specialized;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Shots;

namespace FlickerVoid.Editor.Shots;

/// <summary>
/// The selected shot's time axis: a diamond for each key at its time, the selected one in the accent colour, and the scrubbed
/// time as a line. Dragging a key retimes it; pressing or dragging anywhere else scrubs.
/// </summary>
public sealed class ShotTimelineStrip : Control
{
    public static readonly StyledProperty<ShotTimelineViewModel?> TimelineProperty =
        AvaloniaProperty.Register<ShotTimelineStrip, ShotTimelineViewModel?>(nameof(Timeline));

    private const double Inset = 8;
    private const double KeyRadius = 5;
    private const double GrabPixels = 7;

    private static readonly IBrush AxisBrush = new SolidColorBrush(Colors.White, 0.35);
    private static readonly IBrush KeyBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0xCC, 0xFF));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0x95, 0x5A));
    private static readonly IPen ScrubPen = new Pen(new SolidColorBrush(Color.FromRgb(0xD9, 0xB2, 0x3A)), 2);

    private int? _dragged;
    private bool _scrubbing;

    static ShotTimelineStrip()
    {
        AffectsRender<ShotTimelineStrip>(TimelineProperty);
    }

    public ShotTimelineStrip()
    {
        ToolTip.SetTip(this, "Drag a key to retime it; press or drag elsewhere to scrub the camera preview.");
    }

    public ShotTimelineViewModel? Timeline
    {
        get => GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        context.DrawRectangle(Brushes.Transparent, null, bounds);
        double middle = bounds.Height / 2;
        Pen axis = new(AxisBrush, 1);
        context.DrawLine(axis, new Point(Inset, middle), new Point(bounds.Width - Inset, middle));
        if (Timeline is not { } timeline)
        {
            return;
        }

        foreach (ShotKeyItem key in timeline.Keys)
        {
            double x = ToX(key.Time, timeline);
            IBrush fill = KeyBrush;
            if (key.IsSelected)
            {
                fill = SelectedBrush;
            }

            StreamGeometry diamond = CreateDiamond(new Point(x, middle));
            context.DrawGeometry(fill, null, diamond);
        }

        double scrub = ToX(timeline.ScrubTime, timeline);
        context.DrawLine(ScrubPen, new Point(scrub, 2), new Point(scrub, bounds.Height - 2));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TimelineProperty)
        {
            return;
        }

        if (change.OldValue is ShotTimelineViewModel old)
        {
            old.PropertyChanged -= HandleTimelineChanged;
            old.Keys.CollectionChanged -= HandleKeysChanged;
        }

        if (change.NewValue is ShotTimelineViewModel timeline)
        {
            timeline.PropertyChanged += HandleTimelineChanged;
            timeline.Keys.CollectionChanged += HandleKeysChanged;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Timeline is not { } timeline || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point position = e.GetPosition(this);
        _dragged = FindKey(position.X, timeline);
        timeline.SelectKey(_dragged);
        _scrubbing = _dragged is null;
        if (_scrubbing)
        {
            timeline.ScrubTime = ToTime(position.X, timeline);
        }

        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Timeline is not { } timeline)
        {
            return;
        }

        Point position = e.GetPosition(this);
        float time = ToTime(position.X, timeline);
        if (_dragged is { } key)
        {
            timeline.MoveKeyTime(key, time);
            timeline.SelectKey(key);
        }
        else if (_scrubbing)
        {
            timeline.ScrubTime = time;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragged = null;
        _scrubbing = false;
        e.Pointer.Capture(null);
    }

    private void HandleTimelineChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void HandleKeysChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private int? FindKey(double x, ShotTimelineViewModel timeline)
    {
        foreach (ShotKeyItem key in timeline.Keys)
        {
            double keyX = ToX(key.Time, timeline);
            if (Math.Abs(keyX - x) <= GrabPixels)
            {
                return key.Index;
            }
        }

        return null;
    }

    private double ToX(float time, ShotTimelineViewModel timeline)
    {
        double width = Math.Max(Bounds.Width - (2 * Inset), 1);
        return Inset + (width * time / timeline.Duration);
    }

    /// <summary>The time at a point across the strip; dragging past the end stretches the timeline.</summary>
    private float ToTime(double x, ShotTimelineViewModel timeline)
    {
        double width = Math.Max(Bounds.Width - (2 * Inset), 1);
        double share = (x - Inset) / width;
        double time = share * timeline.Duration;
        return (float)Math.Max(time, 0);
    }

    private static StreamGeometry CreateDiamond(Point centre)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(new Point(centre.X, centre.Y - KeyRadius), true);
        context.LineTo(new Point(centre.X + KeyRadius, centre.Y));
        context.LineTo(new Point(centre.X, centre.Y + KeyRadius));
        context.LineTo(new Point(centre.X - KeyRadius, centre.Y));
        context.EndFigure(true);
        return geometry;
    }
}
