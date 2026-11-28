using System.Globalization;

using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// The regions of a timeline as the designs draw them: a tab on the ruler (78% of the colour, dark ink, a 15 px key box, the name
/// and the length, a note at its end) and a band down the lanes, washed by its state: playing at 9% with an inner outline, queued
/// with a dashed outline and a glow that pulses on the beat, idle dimmed with a pale tab.
/// </summary>
public static class RegionDrawing
{
    public const double TabHeight = 24;

    public const double KeyBox = 15;

    public const double KeyRadius = 3;

    public static void DrawTab(DrawingContext context, TimelineRegion region, Rect tab, string unit, StyledColors colors)
    {
        Color tint = colors.Tint;
        Color background = ThemeColors.Mix(tint, colors.Panel, 0.78);
        Color ink = colors.Ink;
        if (region.State == RegionState.Idle && !region.IsSelected)
        {
            background = ThemeColors.Mix(tint, colors.Panel, 0.38);
            ink = colors.Text;
        }

        using DrawingContext.PushedState clip = context.PushClip(tab);
        context.DrawRectangle(new SolidColorBrush(background), null, tab);
        double x = tab.X + 8;
        double middle = tab.Y + (tab.Height / 2);
        if (!string.IsNullOrEmpty(region.Key))
        {
            Rect box = new(x, middle - (KeyBox / 2), KeyBox, KeyBox);
            Color boxColor = ThemeColors.Fade(colors.Ink, 0.2);
            context.DrawRectangle(new SolidColorBrush(boxColor), null, box, KeyRadius, KeyRadius);
            FormattedText key = TimelineDrawing.Text(region.Key, 10.5, ink, FontWeight.Bold);
            context.DrawText(key, new Point(box.X + ((KeyBox - key.Width) / 2), middle - (key.Height / 2)));
            x = box.Right + 6;
        }

        FormattedText name = TimelineDrawing.Text(region.Name, 11.5, ink, FontWeight.Bold);
        context.DrawText(name, new Point(x, middle - (name.Height / 2)));
        x += name.Width + 6;
        Color soft = ThemeColors.Fade(ink, 0.75);
        string length = FormatLength(region.Length, unit);
        FormattedText lengthText = TimelineDrawing.Text(length, 10.5, soft, FontWeight.Medium);
        context.DrawText(lengthText, new Point(x, middle - (lengthText.Height / 2)));
        if (string.IsNullOrEmpty(region.Note))
        {
            return;
        }

        FormattedText note = TimelineDrawing.Text(region.Note, 10.5, soft, FontWeight.Medium);
        double noteLeft = Math.Max(tab.Right - 8 - note.Width, x + lengthText.Width + 6);
        context.DrawText(note, new Point(noteLeft, middle - (note.Height / 2)));
    }

    /// <summary>Draws the band over the lanes; <paramref name="pulse"/> from 0 to 1 sets a queued band's glow.</summary>
    public static void DrawBand(DrawingContext context, TimelineRegion region, Rect band, StyledColors colors, double pulse)
    {
        Color tint = colors.Tint;
        switch (region.State)
        {
            case RegionState.Live:
                DrawLive(context, region, band, tint);
                break;
            case RegionState.Queued:
                DrawQueued(context, band, tint, colors, pulse);
                break;
            default:
                DrawIdle(context, region, band, tint, colors);
                break;
        }
    }

    /// <summary>A region being drawn on the ruler: an accent wash with a dashed outline.</summary>
    public static void DrawDraft(DrawingContext context, Rect band, Color accent)
    {
        Color wash = ThemeColors.Fade(accent, 0.12);
        context.DrawRectangle(new SolidColorBrush(wash), null, band);
        Pen dashed = new(new SolidColorBrush(accent), 1) { DashStyle = DashStyle.Dash };
        context.DrawRectangle(null, dashed, band.Deflate(0.5));
    }

    public static string FormatLength(double length, string unit)
    {
        string number = length.ToString("0.##", CultureInfo.InvariantCulture);
        string name = unit;
        if (Math.Abs(length - 1) < 1e-9 && unit.EndsWith('s'))
        {
            name = unit[..^1];
        }

        return string.Create(CultureInfo.InvariantCulture, $"{number} {name}");
    }

    private static void DrawLive(DrawingContext context, TimelineRegion region, Rect band, Color tint)
    {
        double share = 0.09;
        if (region.IsSelected)
        {
            share = 0.16;
        }

        context.DrawRectangle(new SolidColorBrush(ThemeColors.Fade(tint, share)), null, band);
        Pen inner = new(new SolidColorBrush(tint), 1);
        context.DrawRectangle(null, inner, band.Deflate(0.5));
        DrawEdges(context, band, tint, ThemeColors.Fade(tint, 0.6));
    }

    private static void DrawIdle(DrawingContext context, TimelineRegion region, Rect band, Color tint, StyledColors colors)
    {
        Color wash = ThemeColors.Fade(colors.Base, 0.55);
        Color left = ThemeColors.Mix(tint, colors.Panel, 0.55);
        if (region.IsSelected)
        {
            wash = ThemeColors.Fade(tint, 0.16);
            left = tint;
        }

        context.DrawRectangle(new SolidColorBrush(wash), null, band);
        DrawEdges(context, band, left, ThemeColors.Fade(tint, 0.6));
    }

    private static void DrawQueued(DrawingContext context, Rect band, Color tint, StyledColors colors, double pulse)
    {
        Color wash = ThemeColors.Fade(colors.Base, 0.3);
        context.DrawRectangle(new SolidColorBrush(wash), null, band);
        double strength = 0.55 + (0.45 * pulse);
        for (int step = 0; step < 8; step++)
        {
            double share = 0.22 * strength * (1 - (step / 8.0));
            Pen glow = new(new SolidColorBrush(ThemeColors.Fade(tint, share)), 5);
            Rect ring = band.Deflate(2.5 + (step * 5));
            if (ring.Width <= 0 || ring.Height <= 0)
            {
                break;
            }

            context.DrawRectangle(null, glow, ring);
        }

        Pen halo = new(new SolidColorBrush(ThemeColors.Fade(tint, 0.28 * strength)), 4);
        context.DrawRectangle(null, halo, band.Inflate(2));
        Pen dashed = new(new SolidColorBrush(tint), 2) { DashStyle = new DashStyle([3, 2], 0) };
        Rect line = band.Deflate(1);
        context.DrawLine(dashed, line.TopLeft, line.BottomLeft);
        context.DrawLine(dashed, line.BottomLeft, line.BottomRight);
        context.DrawLine(dashed, line.BottomRight, line.TopRight);
    }

    /// <summary>The 2 px edges of a band: its start in one colour, its end in another.</summary>
    private static void DrawEdges(DrawingContext context, Rect band, Color start, Color end)
    {
        context.DrawRectangle(new SolidColorBrush(start), null, new Rect(band.X, band.Y, 2, band.Height));
        context.DrawRectangle(new SolidColorBrush(end), null, new Rect(band.Right - 2, band.Y, 2, band.Height));
    }
}
