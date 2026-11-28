using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>
/// The part of time and value the curve view shows on its screen: time runs across, value upward. Its time half is a shared
/// <see cref="TimeRange"/>, so the rulers and strips over the curves scroll and zoom with them. It pans by screen pixels, zooms
/// either axis about a screen point, and frames a box.
/// </summary>
public sealed partial class CurveRange : ObservableObject
{
    /// <summary>How much one wheel notch zooms.</summary>
    private const double ZoomPerStep = TimeRange.ZoomPerStep;

    /// <summary>The narrowest span either axis shows, so a flat or single key still frames.</summary>
    private const double MinSpan = 1e-3;

    /// <summary>The share of the value span framing leaves above and below.</summary>
    private const double DefaultMargin = 0.1;

    public CurveRange()
    {
        Time.PropertyChanged += HandleTimeChanged;
    }

    /// <summary>The time shown, shared with every control over the same curves.</summary>
    public TimeRange Time { get; } = new();

    public double MinTime => Time.MinTime;

    public double MaxTime => Time.MaxTime;

    [ObservableProperty]
    public partial double MinValue { get; private set; }

    [ObservableProperty]
    public partial double MaxValue { get; private set; } = 1;

    /// <summary>The screen's width in pixels.</summary>
    public double Width => Time.Width;

    /// <summary>The screen's height in pixels.</summary>
    public double Height { get; private set; } = 1;

    public void Resize(double width, double height)
    {
        Time.Resize(width);
        Height = Math.Max(height, 1);
    }

    public CurvePoint ToScreen(double time, double value)
    {
        double x = Time.ToX(time);
        double y = Height - ((value - MinValue) / (MaxValue - MinValue) * Height);
        return new CurvePoint(x, y);
    }

    public double ToTime(double x) => Time.ToTime(x);

    public double ToValue(double y) => MinValue + ((Height - y) / Height * (MaxValue - MinValue));

    /// <summary>Moves what is shown with the pointer: a drag to the right shows earlier times, a drag down higher values.</summary>
    public void Pan(double x, double y)
    {
        double value = y / Height * (MaxValue - MinValue);
        Time.Pan(x);
        MinValue += value;
        MaxValue += value;
    }

    /// <summary>Zooms in by whole or partial wheel steps, or out with negative ones, keeping the time and value under the point still.</summary>
    public void ZoomAt(double x, double y, double steps, CurveAxes axes)
    {
        if (axes.HasFlag(CurveAxes.Time))
        {
            Time.ZoomAt(x, steps);
        }

        if (axes.HasFlag(CurveAxes.Value))
        {
            double scale = Math.Pow(ZoomPerStep, -steps);
            double anchor = ToValue(y);
            MinValue = anchor + ((MinValue - anchor) * scale);
            MaxValue = anchor + ((MaxValue - anchor) * scale);
        }
    }

    /// <summary>Shows the times whole and the values with a share of their span left above and below.</summary>
    public void Frame(double minTime, double maxTime, double minValue, double maxValue, double margin = DefaultMargin)
    {
        Time.Frame(minTime, maxTime);
        double span = maxValue - minValue;
        if (span < MinSpan)
        {
            double half = Math.Max(Math.Abs(minValue) * 0.5, 0.5);
            minValue -= half;
            maxValue += half;
            span = maxValue - minValue;
        }

        MinValue = minValue - (span * margin);
        MaxValue = maxValue + (span * margin);
    }

    /// <summary>Tells the curve view's watchers of a time change as its own.</summary>
    private void HandleTimeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimeRange.MinTime) or nameof(TimeRange.MaxTime))
        {
            OnPropertyChanged(e.PropertyName);
        }
    }
}
