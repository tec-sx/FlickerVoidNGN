using System.ComponentModel;
using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Editor.Core.Timelines;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Curves;

/// <summary>
/// A color curve as a gradient across a time range, with a marker under each key: a press selects a
/// key and a drag moves it (Alt without snapping), a double-click adds one, Delete removes the selected one.
/// </summary>
public sealed class ColorStrip : Control
{
    public static readonly StyledProperty<ColorTrack?> TrackProperty =
        AvaloniaProperty.Register<ColorStrip, ColorTrack?>(nameof(Track));

    /// <summary>The times shown, shared with the ruler and the curve view so all scroll and zoom together.</summary>
    public static readonly StyledProperty<TimeRange?> RangeProperty =
        AvaloniaProperty.Register<ColorStrip, TimeRange?>(nameof(Range));

    private const double MarkerHeight = 8;
    private const double SampleStep = 2;
    private const double ReachPixels = 7;

    private static readonly IBrush MarkerBrush = new SolidColorBrush(Color.Parse("#EDE5D8"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#F5A639"));
    private static readonly IPen MarkerPen = new Pen(new SolidColorBrush(Color.Parse("#0B0705")), 1);

    private ColorTrack? _watchedTrack;
    private TimeRange? _watchedRange;
    private bool _dragging;

    static ColorStrip()
    {
        AffectsRender<ColorStrip>(TrackProperty, RangeProperty);
        FocusableProperty.OverrideDefaultValue<ColorStrip>(true);
    }

    public ColorTrack? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public TimeRange? Range
    {
        get => GetValue(RangeProperty);
        set => SetValue(RangeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Track is not { } track || Range is not { } range)
        {
            return;
        }

        double gradientHeight = Math.Max(Bounds.Height - MarkerHeight, 1);
        for (double x = 0; x < Bounds.Width; x += SampleStep)
        {
            double time = ToTime(range, x + (SampleStep / 2));
            Vector3 color = track.Evaluate((float)time);
            IBrush brush = new SolidColorBrush(ToColor(color));
            context.DrawRectangle(brush, null, new Rect(x, 0, SampleStep + 0.5, gradientHeight));
        }

        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            ColorKey key = track.Curve.Keys[i];
            double x = ToX(range, key.Time);
            IBrush fill = track.Selected == i ? SelectedBrush : MarkerBrush;
            StreamGeometry marker = CreateMarker(x, gradientHeight);
            context.DrawGeometry(fill, MarkerPen, marker);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackProperty)
        {
            _watchedTrack?.PropertyChanged -= HandleChanged;
            _watchedTrack = Track;
            _watchedTrack?.PropertyChanged += HandleChanged;
        }
        else if (change.Property == RangeProperty)
        {
            _watchedRange?.PropertyChanged -= HandleChanged;
            _watchedRange = Range;
            _watchedRange?.PropertyChanged += HandleChanged;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Track is not { } track || Range is not { } range || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        double x = e.GetPosition(this).X;
        double time = ToTime(range, x);
        if (e.ClickCount == 2)
        {
            track.AddKey(time);
        }
        else
        {
            double reach = ToTime(range, x + ReachPixels) - time;
            _dragging = track.Press(time, reach);
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || Track is not { } track || Range is not { } range)
        {
            return;
        }

        double time = ToTime(range, e.GetPosition(this).X);
        bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        track.Drag(time, snap);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Track?.Release();
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Delete && Track is { } track)
        {
            track.DeleteSelected();
            e.Handled = true;
        }
    }

    /// <summary>A mood's linear color as its field's color picker shows it: encoded to sRGB, clamped.</summary>
    public static Color ToColor(Vector3 linear)
    {
        byte red = ToSrgbByte(linear.X);
        byte green = ToSrgbByte(linear.Y);
        byte blue = ToSrgbByte(linear.Z);
        return Color.FromRgb(red, green, blue);
    }

    private static byte ToSrgbByte(float linear)
    {
        float clamped = Math.Clamp(linear, 0, 1);
        float srgb;
        if (clamped <= 0.0031308f)
        {
            srgb = clamped * 12.92f;
        }
        else
        {
            srgb = (1.055f * MathF.Pow(clamped, 1 / 2.4f)) - 0.055f;
        }

        return (byte)MathF.Round(srgb * 255);
    }

    // The range's width is the curve view's, so its times are mapped across this strip's own width.
    private double ToTime(TimeRange range, double x) => range.MinTime + (x / Math.Max(Bounds.Width, 1) * (range.MaxTime - range.MinTime));

    private double ToX(TimeRange range, double time) => (time - range.MinTime) / (range.MaxTime - range.MinTime) * Bounds.Width;

    private static StreamGeometry CreateMarker(double x, double top)
    {
        StreamGeometry geometry = new();
        using StreamGeometryContext context = geometry.Open();
        context.BeginFigure(new Point(x, top), true);
        context.LineTo(new Point(x + (MarkerHeight * 0.6), top + MarkerHeight));
        context.LineTo(new Point(x - (MarkerHeight * 0.6), top + MarkerHeight));
        context.EndFigure(true);
        return geometry;
    }

    private void HandleChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();
}
