using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>A key: the marker's diamond and name, in the middle of its lane's height.</summary>
public sealed class KeyPainter : ILaneItemPainter
{
    public void Paint(DrawingContext context, TimelineItem item, Rect span, LanePaint paint)
    {
        Point centre = new(span.X, span.Y + (span.Height / 2));
        MarkerPainter.Draw(context, item, centre, paint);
    }
}
