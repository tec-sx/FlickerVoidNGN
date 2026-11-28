using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Timelines;

/// <summary>
/// The span of time a timeline shows across its width in pixels. It pans by pixels, zooms about the time under a point within its
/// limits and frames a span. Every control of one view shares one range, so they scroll and zoom together; each change is told once.
/// </summary>
public sealed class TimeRange : ObservableObject
{
    /// <summary>How much one wheel notch zooms.</summary>
    public const double ZoomPerStep = 1.15;

    public double MinTime { get; private set; }

    public double MaxTime { get; private set; } = 24;

    /// <summary>The screen's width in pixels, never less than one.</summary>
    public double Width { get; private set; } = 1;

    /// <summary>The narrowest span shown, so a point still frames.</summary>
    public double MinSpan { get; init; } = 1e-3;

    /// <summary>The widest span shown.</summary>
    public double MaxSpan { get; init; } = 1e6;

    /// <summary>The earliest time shown, or null when the view may show any time (hours before midnight, for one).</summary>
    public double? Earliest { get; init; }

    public double Span => MaxTime - MinTime;

    public double PixelsPerUnit => Width / Span;

    /// <summary>Raised once after any change of the shown span or the width.</summary>
    public event Action? Changed;

    public void Resize(double width)
    {
        double clamped = Math.Max(width, 1);
        if (clamped == Width)
        {
            return;
        }

        Width = clamped;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(PixelsPerUnit));
        Changed?.Invoke();
    }

    public double ToX(double time) => (time - MinTime) / Span * Width;

    public double ToTime(double x) => MinTime + (x / Width * Span);

    /// <summary>Moves what is shown with the pointer: a drag to the right shows earlier times.</summary>
    public void Pan(double pixels)
    {
        double time = pixels / Width * Span;
        Show(MinTime - time, MaxTime - time);
    }

    /// <summary>Zooms in by whole or partial wheel steps, or out with negative ones, keeping the time under the point still.</summary>
    public void ZoomAt(double x, double steps)
    {
        double anchor = ToTime(x);
        double scale = Math.Pow(ZoomPerStep, -steps);
        double span = Math.Clamp(Span * scale, MinSpan, MaxSpan);
        double share = (anchor - MinTime) / Span;
        double minTime = anchor - (share * span);
        Show(minTime, minTime + span);
    }

    /// <summary>
    /// Shows a span whole, with a share of its length and a number of pixels left on both sides; a point gets a unit either side.
    /// </summary>
    public void Frame(double start, double end, double margin = 0, double inset = 0)
    {
        if (end - start < MinSpan)
        {
            start -= 1;
            end += 1;
        }

        double pad = (end - start) * margin;
        start -= pad;
        end += pad;
        double usable = Width - (2 * inset);
        if (inset > 0 && usable >= 1)
        {
            double perPixel = (end - start) / usable;
            start -= inset * perPixel;
            end += inset * perPixel;
        }

        Show(start, end);
    }

    /// <summary>Shows exactly a span, widened or narrowed about its middle to the limits and moved after the earliest time.</summary>
    public void Show(double minTime, double maxTime)
    {
        double span = maxTime - minTime;
        double clamped = Math.Clamp(span, MinSpan, MaxSpan);
        if (clamped != span)
        {
            double middle = (minTime + maxTime) * 0.5;
            minTime = middle - (clamped * 0.5);
            maxTime = middle + (clamped * 0.5);
        }

        if (Earliest is { } earliest && minTime < earliest)
        {
            maxTime += earliest - minTime;
            minTime = earliest;
        }

        if (minTime == MinTime && maxTime == MaxTime)
        {
            return;
        }

        MinTime = minTime;
        MaxTime = maxTime;
        OnPropertyChanged(nameof(MinTime));
        OnPropertyChanged(nameof(MaxTime));
        OnPropertyChanged(nameof(Span));
        OnPropertyChanged(nameof(PixelsPerUnit));
        Changed?.Invoke();
    }
}
