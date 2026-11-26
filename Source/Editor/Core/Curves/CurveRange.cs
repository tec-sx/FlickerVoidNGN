using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Curves;

/// <summary>
/// The part of time and value the curve view shows on its screen: time runs across, value upward. It pans by screen pixels,
/// zooms either axis about a screen point, and frames a box.
/// </summary>
public sealed partial class CurveRange : ObservableObject
{
    /// <summary>How much one wheel notch zooms.</summary>
    private const double ZoomPerStep = 1.15;

    /// <summary>The narrowest span either axis shows, so a flat or single key still frames.</summary>
    private const double MinSpan = 1e-3;

    /// <summary>The share of the value span framing leaves above and below.</summary>
    private const double DefaultMargin = 0.1;

    [ObservableProperty]
    public partial double MinTime { get; private set; }

    [ObservableProperty]
    public partial double MaxTime { get; private set; } = 24;

    [ObservableProperty]
    public partial double MinValue { get; private set; }

    [ObservableProperty]
    public partial double MaxValue { get; private set; } = 1;

    /// <summary>The screen's width in pixels.</summary>
    public double Width { get; private set; } = 1;

    /// <summary>The screen's height in pixels.</summary>
    public double Height { get; private set; } = 1;

    public void Resize(double width, double height)
    {
        Width = Math.Max(width, 1);
        Height = Math.Max(height, 1);
    }

    public CurvePoint ToScreen(double time, double value)
    {
        double x = (time - MinTime) / (MaxTime - MinTime) * Width;
        double y = Height - ((value - MinValue) / (MaxValue - MinValue) * Height);
        return new CurvePoint(x, y);
    }

    public double ToTime(double x) => MinTime + (x / Width * (MaxTime - MinTime));

    public double ToValue(double y) => MinValue + ((Height - y) / Height * (MaxValue - MinValue));

    /// <summary>Moves what is shown with the pointer: a drag to the right shows earlier times, a drag down higher values.</summary>
    public void Pan(double x, double y)
    {
        double time = x / Width * (MaxTime - MinTime);
        double value = y / Height * (MaxValue - MinValue);
        MinTime -= time;
        MaxTime -= time;
        MinValue += value;
        MaxValue += value;
    }

    /// <summary>Zooms in by whole or partial wheel steps, or out with negative ones, keeping the time and value under the point still.</summary>
    public void ZoomAt(double x, double y, double steps, CurveAxes axes)
    {
        double scale = Math.Pow(ZoomPerStep, -steps);
        if (axes.HasFlag(CurveAxes.Time))
        {
            double anchor = ToTime(x);
            MinTime = anchor + ((MinTime - anchor) * scale);
            MaxTime = anchor + ((MaxTime - anchor) * scale);
        }

        if (axes.HasFlag(CurveAxes.Value))
        {
            double anchor = ToValue(y);
            MinValue = anchor + ((MinValue - anchor) * scale);
            MaxValue = anchor + ((MaxValue - anchor) * scale);
        }
    }

    /// <summary>Shows the times whole and the values with a share of their span left above and below.</summary>
    public void Frame(double minTime, double maxTime, double minValue, double maxValue, double margin = DefaultMargin)
    {
        if (maxTime - minTime < MinSpan)
        {
            minTime -= 1;
            maxTime += 1;
        }

        double span = maxValue - minValue;
        if (span < MinSpan)
        {
            double half = Math.Max(Math.Abs(minValue) * 0.5, 0.5);
            minValue -= half;
            maxValue += half;
            span = maxValue - minValue;
        }

        MinTime = minTime;
        MaxTime = maxTime;
        MinValue = minValue - (span * margin);
        MaxValue = maxValue + (span * margin);
    }
}
