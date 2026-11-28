using Avalonia.Input;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>The wheel and the keys every timeline control answers alike.</summary>
public static class TimelineInput
{
    /// <summary>How many pixels one wheel notch pans with Shift.</summary>
    public const double PanPerNotch = 60;

    /// <summary>Zooms about the pointer, or pans with Shift; leaves the wheel to the scroller when asked to with Ctrl unpressed.</summary>
    public static void Wheel(TimelineViewModel timeline, PointerWheelEventArgs e, double x, bool zoomNeedsControl = false)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            double notches = e.Delta.Y;
            if (notches == 0)
            {
                notches = e.Delta.X;
            }

            timeline.Range.Pan(notches * PanPerNotch);
            e.Handled = true;
            return;
        }

        if (zoomNeedsControl && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        timeline.Range.ZoomAt(x, e.Delta.Y);
        e.Handled = true;
    }

    /// <summary>Arrows nudge the selection by a snap step, Delete removes it, Home and End move the cursor to the ends.</summary>
    public static void Keys(TimelineViewModel timeline, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                TimelineEdit.Nudge(timeline, -1);
                e.Handled = true;
                break;
            case Key.Right:
                TimelineEdit.Nudge(timeline, 1);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                TimelineEdit.DeleteSelected(timeline);
                e.Handled = true;
                break;
            case Key.Home:
                timeline.MoveCursor(0);
                e.Handled = true;
                break;
            case Key.End:
                timeline.MoveCursor(timeline.End);
                e.Handled = true;
                break;
        }
    }
}
