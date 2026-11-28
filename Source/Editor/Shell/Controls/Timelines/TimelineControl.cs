using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// What every drawn timeline control shares: the timeline it shows, drawing again when it or its range changes, and following the
/// <see cref="BeatPulse"/> while it is shown and something on it waits for its beat.
/// </summary>
public abstract class TimelineControl : Control
{
    public static readonly StyledProperty<TimelineViewModel?> TimelineProperty =
        AvaloniaProperty.Register<TimelineControl, TimelineViewModel?>(nameof(Timeline));

    private TimelineViewModel? _watched;
    private bool _pulsing;
    private bool _attached;

    public TimelineViewModel? Timeline
    {
        get => GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    /// <summary>The beat's pulse now, from 0 to 1.</summary>
    protected static double Pulse => BeatPulse.Value;

    /// <summary>Whether something drawn waits for its beat and so pulses.</summary>
    protected virtual bool NeedsPulse(TimelineViewModel timeline)
    {
        foreach (TimelineRegion region in timeline.Regions)
        {
            if (region.State == RegionState.Queued)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TimelineProperty)
        {
            return;
        }

        Watch(Timeline);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Watch(Timeline);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Watch(null);
        SetPulsing(false);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Timeline?.Range.Resize(e.NewSize.Width);
    }

    /// <summary>Called when the timeline changes, before drawing again.</summary>
    protected virtual void OnTimelineChanged(string? property)
    {
    }

    private void Watch(TimelineViewModel? timeline)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= HandleTimelineChanged;
            _watched.Range.Changed -= HandleRangeChanged;
        }

        _watched = timeline;
        if (_watched is null)
        {
            return;
        }

        _watched.PropertyChanged += HandleTimelineChanged;
        _watched.Range.Changed += HandleRangeChanged;
        if (Bounds.Width > 0)
        {
            _watched.Range.Resize(Bounds.Width);
        }

        SetPulsing(NeedsPulse(_watched));
    }

    private void HandleTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnTimelineChanged(e.PropertyName);
        if (_watched is not null)
        {
            SetPulsing(NeedsPulse(_watched));
        }

        if (e.PropertyName == nameof(TimelineViewModel.Revision))
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    private void HandleRangeChanged() => InvalidateVisual();

    private void SetPulsing(bool pulsing)
    {
        bool shown = pulsing && _attached;
        if (shown == _pulsing)
        {
            return;
        }

        _pulsing = shown;
        if (shown)
        {
            BeatPulse.Listen(InvalidateVisual);
        }
        else
        {
            BeatPulse.Ignore(InvalidateVisual);
        }
    }
}
