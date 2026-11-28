using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// What a timeline draws over all its lanes at once (rule R28): the regions' bands from top to bottom, washed and outlined by
/// their state with queued ones pulsing on the beat, the region being drawn, and the position line. It takes no clicks: the
/// lanes and the ruler under it read the pointer.
/// </summary>
public sealed class TimelineOverlay : TimelineControl
{
    public static readonly StyledProperty<bool> ShowsPositionProperty = AvaloniaProperty.Register<TimelineOverlay, bool>(nameof(ShowsPosition), true);

    static TimelineOverlay()
    {
        AffectsRender<TimelineOverlay>(ShowsPositionProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TimelineOverlay>(false);
    }

    public TimelineOverlay()
    {
        ClipToBounds = true;
    }

    public bool ShowsPosition
    {
        get => GetValue(ShowsPositionProperty);
        set => SetValue(ShowsPositionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Timeline is not { } timeline)
        {
            return;
        }

        Rect bounds = new(Bounds.Size);
        StyledColors colors = StyledColors.Read(this);
        double pulse = Pulse;
        foreach (TimelineRegion region in timeline.Regions)
        {
            Rect band = GetBand(timeline, region.Start, region.End, bounds);
            if (band.Width <= 0 || band.Right < 0 || band.Left > bounds.Width)
            {
                continue;
            }

            Color tint = ThemeColors.Find(this, region.Tint);
            StyledColors tinted = colors.WithTint(tint);
            RegionDrawing.DrawBand(context, region, band, tinted, pulse);
        }

        if (timeline.Draft is { } draft)
        {
            Rect band = GetBand(timeline, draft.Start, draft.End, bounds);
            RegionDrawing.DrawDraft(context, band, colors.Accent);
        }

        if (!ShowsPosition)
        {
            return;
        }

        double time = TimelineDrawing.PositionOf(timeline);
        double x = timeline.Range.ToX(time);
        TimelineDrawing.Position(context, x, bounds.Height, 0, null, colors.Accent, colors.AccentInk);
    }

    private static Rect GetBand(TimelineViewModel timeline, double start, double end, Rect bounds)
    {
        double left = Math.Round(timeline.Range.ToX(start));
        double right = Math.Round(timeline.Range.ToX(end));
        return new Rect(left, 0, Math.Max(right - left, 0), bounds.Height);
    }
}
