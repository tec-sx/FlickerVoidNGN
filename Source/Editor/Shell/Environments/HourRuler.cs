using System.ComponentModel;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Curves;

namespace FlickerVoid.Editor.Environments;

/// <summary>
/// The hours of the day across a curve view's times, with the cursor at <see cref="Hour"/>; pressing or dragging moves the
/// cursor there, snapped to a quarter hour unless Alt is held, wrapped into the day.
/// </summary>
public sealed class HourRuler : Control
{
    public static readonly StyledProperty<CurveRange?> RangeProperty =
        AvaloniaProperty.Register<HourRuler, CurveRange?>(nameof(Range));

    public static readonly StyledProperty<float> HourProperty =
        AvaloniaProperty.Register<HourRuler, float>(nameof(Hour), defaultBindingMode: BindingMode.TwoWay);

    private const float Day = 24;
    private const float Snap = 0.25f;

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#1D1610"));
    private static readonly IBrush NightBrush = new SolidColorBrush(Color.Parse("#0B0705"));
    private static readonly IPen TickPen = new Pen(new SolidColorBrush(Color.Parse("#6B6052")), 1);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#B6AB99"));
    private static readonly IPen CursorPen = new Pen(new SolidColorBrush(Color.Parse("#D9B23A")), 2);
    private static readonly IBrush CursorBrush = new SolidColorBrush(Color.Parse("#D9B23A"));

    private CurveRange? _watched;
    private bool _dragging;

    static HourRuler()
    {
        AffectsRender<HourRuler>(RangeProperty, HourProperty);
    }

    public HourRuler()
    {
        ToolTip.SetTip(this, "Drag to move the hour (Alt: no snapping).");
    }

    public CurveRange? Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public float Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        context.DrawRectangle(Background, null, bounds);
        if (Range is not { } range)
        {
            return;
        }

        double first = Math.Floor(range.MinTime);
        for (double time = first; time <= range.MaxTime; time += 1)
        {
            double x = ToX(range, time);
            double hour = ((time % Day) + Day) % Day;
            bool major = Math.Abs(hour % 3) < 1e-6;
            double tick = major ? bounds.Height * 0.5 : bounds.Height * 0.25;
            context.DrawLine(TickPen, new Point(x, bounds.Height - tick), new Point(x, bounds.Height));
            if (major)
            {
                string label = $"{(int)Math.Round(hour):00}:00";
                FormattedText text = new(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, LabelBrush);
                context.DrawText(text, new Point(x + 2, 1));
            }
        }

        DrawOutsideTheDay(context, range, bounds);
        double cursor = ToX(range, Hour);
        context.DrawLine(CursorPen, new Point(cursor, 0), new Point(cursor, bounds.Height));
        StreamGeometry handle = new();
        using (StreamGeometryContext figure = handle.Open())
        {
            figure.BeginFigure(new Point(cursor - 5, 0), true);
            figure.LineTo(new Point(cursor + 5, 0));
            figure.LineTo(new Point(cursor, 7));
            figure.EndFigure(true);
        }

        context.DrawGeometry(CursorBrush, null, handle);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != RangeProperty)
        {
            return;
        }

        _watched?.PropertyChanged -= HandleRangeChanged;
        _watched = Range;
        _watched?.PropertyChanged += HandleRangeChanged;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        MoveCursor(e);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            MoveCursor(e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void MoveCursor(PointerEventArgs e)
    {
        if (Range is not { } range)
        {
            return;
        }

        double x = e.GetPosition(this).X;
        double time = range.MinTime + (x / Math.Max(Bounds.Width, 1) * (range.MaxTime - range.MinTime));
        float hour = (float)time;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            hour = MathF.Round(hour / Snap) * Snap;
        }

        Hour = ((hour % Day) + Day) % Day;
    }

    /// <summary>Darkens the times shown before midnight and after the day's end, where a looping curve repeats.</summary>
    private void DrawOutsideTheDay(DrawingContext context, CurveRange range, Rect bounds)
    {
        double start = ToX(range, 0);
        double end = ToX(range, Day);
        if (start > 0)
        {
            context.DrawRectangle(NightBrush, null, new Rect(0, 0, start, bounds.Height), 0, 0);
        }

        if (end < bounds.Width)
        {
            context.DrawRectangle(NightBrush, null, new Rect(end, 0, bounds.Width - end, bounds.Height), 0, 0);
        }
    }

    private double ToX(CurveRange range, double time) => (time - range.MinTime) / (range.MaxTime - range.MinTime) * Bounds.Width;

    private void HandleRangeChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
}
