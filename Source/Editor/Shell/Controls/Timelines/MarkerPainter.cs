using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// A marker as the designs draw it: at the lane's foot, a 9 px diamond in the lane's colour at its time and its name 5 px after it
/// in 10.5 px text; a selected one ringed in the text colour with its name bold, a repeated one at 38%.
/// </summary>
public sealed class MarkerPainter : ILaneItemPainter
{
    public const double Side = 9;

    public const double Height = 18;

    public void Paint(DrawingContext context, TimelineItem item, Rect span, LanePaint paint)
    {
        double middle = span.Bottom - (Height / 2);
        Draw(context, item, new Point(span.X, middle), paint);
    }

    /// <summary>Draws the diamond centred on the point and the name after it.</summary>
    public static void Draw(DrawingContext context, TimelineItem item, Point centre, LanePaint paint)
    {
        double opacity = 1;
        if (item.IsRepeat || item.IsDimmed)
        {
            opacity = 0.38;
        }

        using DrawingContext.PushedState faded = context.PushOpacity(opacity);
        Geometry diamond = TimelineDrawing.Diamond(centre, Side);
        Pen? ring = null;
        if (item.IsSelected)
        {
            ring = new Pen(new SolidColorBrush(paint.Text), 1.5);
        }

        context.DrawGeometry(new SolidColorBrush(paint.Tint), ring, diamond);
        if (string.IsNullOrEmpty(item.Label))
        {
            return;
        }

        FontWeight weight = FontWeight.Normal;
        if (item.IsSelected)
        {
            weight = FontWeight.SemiBold;
        }

        FormattedText text = TimelineDrawing.Text(item.Label, 10.5, paint.Text, weight);
        double left = centre.X + (Side / 2) + 5;
        context.DrawText(text, new Point(left, centre.Y - (text.Height / 2)));
    }
}
