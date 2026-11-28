using System.Globalization;

using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>What the timeline controls draw alike: text, crisp lines, diamonds, the position chip and the shade outside a day.</summary>
public static class TimelineDrawing
{
    public const double ChipWidth = 44;

    public const double ChipHeight = 15;

    public const double ChipRadius = 3;

    private static readonly Typeface Regular = new(FontFamily.Default);
    private static readonly Typeface Medium = new(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);
    private static readonly Typeface SemiBold = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    public static FormattedText Text(string text, double size, Color color, FontWeight weight = FontWeight.Normal)
    {
        Typeface typeface = weight switch
        {
            FontWeight.Medium => Medium,
            FontWeight.SemiBold => SemiBold,
            FontWeight.Bold => Bold,
            _ => Regular,
        };
        SolidColorBrush brush = new(color);
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
    }

    /// <summary>The middle of the pixel a 1 px line at the position covers, so it stays one pixel wide.</summary>
    public static double Crisp(double position) => Math.Floor(position) + 0.5;

    public static void VerticalLine(DrawingContext context, Color color, double x, double top, double bottom)
    {
        Pen pen = new(new SolidColorBrush(color), 1);
        double crisp = Crisp(x);
        context.DrawLine(pen, new Point(crisp, top), new Point(crisp, bottom));
    }

    public static void HorizontalLine(DrawingContext context, Color color, double y, double left, double right)
    {
        Pen pen = new(new SolidColorBrush(color), 1);
        double crisp = Crisp(y);
        context.DrawLine(pen, new Point(left, crisp), new Point(right, crisp));
    }

    /// <summary>A square turned 45 degrees with slightly rounded corners, as the markers and keys of the designs.</summary>
    public static Geometry Diamond(Point centre, double side)
    {
        double half = side * Math.Sqrt(2) / 2;
        StreamGeometry geometry = new();
        using StreamGeometryContext figure = geometry.Open();
        figure.BeginFigure(new Point(centre.X, centre.Y - half), true);
        figure.LineTo(new Point(centre.X + half, centre.Y));
        figure.LineTo(new Point(centre.X, centre.Y + half));
        figure.LineTo(new Point(centre.X - half, centre.Y));
        figure.EndFigure(true);
        return geometry;
    }

    /// <summary>The position line and its chip: a 1 px accent line down the height and the time on a 44 by 15 accent chip.</summary>
    public static void Position(DrawingContext context, double x, double height, double chipTop, string? label, Color accent, Color ink)
    {
        VerticalLine(context, accent, x, 0, height);
        if (label is null)
        {
            return;
        }

        Rect chip = new(Math.Round(x - (ChipWidth / 2)), chipTop, ChipWidth, ChipHeight);
        SolidColorBrush fill = new(accent);
        context.DrawRectangle(fill, null, chip, ChipRadius, ChipRadius);
        FormattedText text = Text(label, 10.5, ink, FontWeight.SemiBold);
        double left = chip.X + ((chip.Width - text.Width) / 2);
        double top = chip.Y + ((chip.Height - text.Height) / 2);
        context.DrawText(text, new Point(left, top));
    }

    /// <summary>Darkens the times before 0 and after one period of a repeating scale, where a looping curve repeats.</summary>
    public static void ShadeOutsidePeriod(DrawingContext context, TimelineViewModel timeline, Rect bounds, Color shade)
    {
        if (timeline.Scale.Period is not { } period)
        {
            return;
        }

        SolidColorBrush brush = new(shade);
        double start = timeline.Range.ToX(0);
        double end = timeline.Range.ToX(period);
        if (start > bounds.Left)
        {
            context.DrawRectangle(brush, null, new Rect(bounds.Left, bounds.Top, start - bounds.Left, bounds.Height));
        }

        if (end < bounds.Right)
        {
            context.DrawRectangle(brush, null, new Rect(end, bounds.Top, bounds.Right - end, bounds.Height));
        }
    }

    /// <summary>The grid of a lane: a line for each tick, phrases full, bars at 55% and beats at 22% of the line colour.</summary>
    public static void Grid(DrawingContext context, IReadOnlyList<TimeTick> ticks, TimeRange range, Rect row, Color line)
    {
        foreach (TimeTick tick in ticks)
        {
            double share = tick.Level switch
            {
                TickLevel.Section => 1,
                TickLevel.Major => 0.55,
                _ => 0.22,
            };
            Color color = ThemeColors.Fade(line, share);
            double x = range.ToX(tick.Time);
            VerticalLine(context, color, x, row.Top, row.Bottom);
        }
    }

    /// <summary>The time to show in the position chip: the playhead while playing, else the cursor.</summary>
    public static double PositionOf(TimelineViewModel timeline)
    {
        if (double.IsNaN(timeline.Playhead))
        {
            return timeline.Cursor;
        }

        return timeline.Playhead;
    }
}
