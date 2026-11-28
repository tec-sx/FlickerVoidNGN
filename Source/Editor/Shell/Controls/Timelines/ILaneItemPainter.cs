using Avalonia;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>Draws one kind of timeline item within its lane's row; a new kind of item is a new painter (rule R28).</summary>
public interface ILaneItemPainter
{
    /// <summary>Draws the item; <paramref name="span"/> runs from its start to its end across the whole row's height.</summary>
    void Paint(DrawingContext context, TimelineItem item, Rect span, LanePaint paint);
}
