using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// A clip as the designs draw it: 5 px from the lane's top and bottom, radius 3, 20% of the lane's colour over the panel with a 55%
/// border, a 14 px title bar at 42%, and the waveform's peaks (the item's look, a float array) at 90% under it. A repeated pass is
/// lighter at 72%, a dimmed one a dashed ghost at 32%, a selected one outlined in the text colour, a queued one dashed and pulsing.
/// </summary>
public sealed class ClipPainter : ILaneItemPainter
{
    public const double Inset = 5;

    public const double Radius = 3;

    public const double TitleHeight = 14;

    public void Paint(DrawingContext context, TimelineItem item, Rect span, LanePaint paint)
    {
        Rect body = new(span.X, span.Y + Inset, Math.Max(span.Width - 1, 1), Math.Max(span.Height - (2 * Inset), 1));
        double opacity = 1;
        if (item.IsRepeat)
        {
            opacity = 0.72;
        }

        if (item.IsDimmed)
        {
            opacity = 0.32;
        }

        using DrawingContext.PushedState faded = context.PushOpacity(opacity);
        Color fill = ThemeColors.Mix(paint.Tint, paint.Panel, 0.2);
        Color border = ThemeColors.Mix(paint.Tint, paint.Panel, 0.55);
        Pen outline = new(new SolidColorBrush(border), 1);
        if (item.IsSelected)
        {
            outline = new Pen(new SolidColorBrush(paint.Text), 1);
        }

        if (item.IsDimmed || item.IsQueued)
        {
            outline.DashStyle = DashStyle.Dash;
        }

        Rect crisp = body.Deflate(0.5);
        context.DrawRectangle(new SolidColorBrush(fill), null, body, Radius, Radius);
        using (context.PushClip(new RoundedRect(body, Radius)))
        {
            DrawTitle(context, item, body, paint);
            if (item.Look is float[] peaks)
            {
                Rect wave = new(body.X, body.Y + TitleHeight, body.Width, Math.Max(body.Height - TitleHeight, 0));
                DrawWave(context, peaks, wave, paint.Tint);
            }
        }

        context.DrawRectangle(null, outline, crisp, Radius, Radius);
        if (item.IsSelected)
        {
            Pen ring = new(new SolidColorBrush(paint.Text), 1);
            context.DrawRectangle(null, ring, body.Inflate(0.5), Radius + 1, Radius + 1);
        }

        if (item.IsQueued)
        {
            Color glow = ThemeColors.Fade(paint.Tint, 0.2 + (0.35 * paint.Pulse));
            Pen halo = new(new SolidColorBrush(glow), 3);
            context.DrawRectangle(null, halo, body.Inflate(2), Radius + 2, Radius + 2);
        }
    }

    private static void DrawTitle(DrawingContext context, TimelineItem item, Rect body, LanePaint paint)
    {
        double share = 0.42;
        Color textColor = paint.Text;
        if (item.IsRepeat)
        {
            share = 0.26;
            textColor = paint.MutedText;
        }

        Color bar = ThemeColors.Mix(paint.Tint, paint.Panel, share);
        Rect title = new(body.X, body.Y, body.Width, TitleHeight);
        context.DrawRectangle(new SolidColorBrush(bar), null, title);
        string? label = item.Label;
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        FormattedText text = TimelineDrawing.Text(label, 10.5, textColor);
        text.MaxTextWidth = Math.Max(title.Width - 10, 1);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        double top = title.Y + ((TitleHeight - text.Height) / 2);
        context.DrawText(text, new Point(title.X + 5, top));
    }

    /// <summary>A bar every 2 px, 55% of the step wide, reaching up and down 44% of the height from the middle by the peak there.</summary>
    private static void DrawWave(DrawingContext context, float[] peaks, Rect wave, Color tint)
    {
        if (peaks.Length == 0 || wave.Height < 2 || wave.Width < 2)
        {
            return;
        }

        int count = Math.Max(4, (int)Math.Floor(wave.Width / 2));
        double step = wave.Width / count;
        Color color = ThemeColors.Fade(tint, 0.9);
        Pen pen = new(new SolidColorBrush(color), step * 0.55);
        double middle = wave.Y + (wave.Height / 2);
        for (int index = 0; index < count; index++)
        {
            double share = (index + 0.5) / count;
            int peak = Math.Min(peaks.Length - 1, (int)(share * peaks.Length));
            double reach = Math.Min(peaks[peak], 1) * wave.Height * 0.44;
            double x = wave.X + (share * wave.Width);
            context.DrawLine(pen, new Point(x, middle - reach), new Point(x, middle + reach));
        }
    }
}
