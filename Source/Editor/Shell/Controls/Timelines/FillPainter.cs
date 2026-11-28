using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// A fill-in or fill-out as the designs draw it: 3 px from the lane's top and bottom, hatched at 135 degrees in 5 px stripes of 34%
/// and 18% of the colour over the panel, a border in the full colour, and its name bold in the middle of its height.
/// </summary>
public sealed class FillPainter : ILaneItemPainter
{
    public const double Inset = 3;

    public const double Stripe = 5;

    public void Paint(DrawingContext context, TimelineItem item, Rect span, LanePaint paint)
    {
        Rect body = new(span.X, span.Y + Inset, Math.Max(span.Width - 1, 1), Math.Max(span.Height - (2 * Inset), 1));
        double opacity = 1;
        if (item.IsDimmed)
        {
            opacity = 0.32;
        }

        using DrawingContext.PushedState faded = context.PushOpacity(opacity);
        Color light = ThemeColors.Mix(paint.Tint, paint.Panel, 0.34);
        Color dark = ThemeColors.Mix(paint.Tint, paint.Panel, 0.18);
        using (context.PushClip(new RoundedRect(body, ClipPainter.Radius)))
        {
            context.DrawRectangle(new SolidColorBrush(dark), null, body);
            DrawStripes(context, body, light);
            DrawTitle(context, item, body, paint);
        }

        Pen outline = new(new SolidColorBrush(paint.Tint), 1);
        if (item.IsQueued)
        {
            outline.DashStyle = DashStyle.Dash;
        }

        context.DrawRectangle(null, outline, body.Deflate(0.5), ClipPainter.Radius, ClipPainter.Radius);
        if (item.IsSelected)
        {
            Pen ring = new(new SolidColorBrush(paint.Text), 2);
            context.DrawRectangle(null, ring, body.Inflate(1), ClipPainter.Radius + 1, ClipPainter.Radius + 1);
        }
    }

    /// <summary>Light stripes 5 px wide every 10 px across the body, running from bottom left to top right.</summary>
    private static void DrawStripes(DrawingContext context, Rect body, Color light)
    {
        double period = Stripe * 2 * Math.Sqrt(2);
        Pen pen = new(new SolidColorBrush(light), Stripe);
        for (double offset = -body.Height; offset < body.Width + body.Height; offset += period)
        {
            Point from = new(body.X + offset, body.Bottom);
            Point to = new(body.X + offset + body.Height, body.Top);
            context.DrawLine(pen, from, to);
        }
    }

    /// <summary>The name in the text colour, bold, in the middle of the fill's height, with a panel-coloured halo to read over the stripes.</summary>
    private static void DrawTitle(DrawingContext context, TimelineItem item, Rect body, LanePaint paint)
    {
        if (string.IsNullOrEmpty(item.Label))
        {
            return;
        }

        FormattedText text = TimelineDrawing.Text(item.Label, 10.5, paint.Text, FontWeight.SemiBold);
        text.MaxTextWidth = Math.Max(body.Width - 10, 1);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;
        Point at = new(body.X + 5, body.Y + ((body.Height - text.Height) / 2));
        Geometry? outline = text.BuildGeometry(at);
        if (outline is not null)
        {
            Pen halo = new(new SolidColorBrush(ThemeColors.Fade(paint.Panel, 0.8)), 3) { LineJoin = PenLineJoin.Round };
            context.DrawGeometry(null, halo, outline);
        }

        context.DrawText(text, at);
    }
}
