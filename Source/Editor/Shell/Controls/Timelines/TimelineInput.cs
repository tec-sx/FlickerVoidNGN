using Avalonia;
using Avalonia.Input;

using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>The wheel and the keys every timeline control answers alike.</summary>
public static class TimelineInput
{
    /// <summary>How many pixels one wheel notch pans with Shift.</summary>
    public const double PanPerNotch = 60;

    /// <summary>What the wheel does with these keys held (rule R28): alone it zooms the time, Ctrl scales the lanes, Shift pans, Alt scrolls.</summary>
    public static TimelineWheel Read(KeyModifiers keys, Vector delta)
    {
        if (keys.HasFlag(KeyModifiers.Alt))
        {
            return TimelineWheel.Scroll;
        }

        if (keys.HasFlag(KeyModifiers.Shift))
        {
            return TimelineWheel.Pan;
        }

        if (keys.HasFlag(KeyModifiers.Control))
        {
            return TimelineWheel.ScaleLanes;
        }

        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
        {
            return TimelineWheel.Pan;
        }

        return TimelineWheel.ZoomTime;
    }

    /// <summary>The notches the wheel turned: up and down, or sideways where it turned only sideways (as Shift makes some systems).</summary>
    public static double Notches(Vector delta)
    {
        if (delta.Y != 0)
        {
            return delta.Y;
        }

        return delta.X;
    }

    /// <summary>
    /// Zooms the time about the pointer or pans it. The kit's lanes keep their heights, so Ctrl and Alt leave the wheel to the
    /// control around them.
    /// </summary>
    public static void Wheel(TimelineViewModel timeline, PointerWheelEventArgs e, double x)
    {
        TimelineWheel wheel = Read(e.KeyModifiers, e.Delta);
        switch (wheel)
        {
            case TimelineWheel.ZoomTime:
                timeline.Range.ZoomAt(x, e.Delta.Y);
                e.Handled = true;
                break;
            case TimelineWheel.Pan:
                double notches = Notches(e.Delta);
                timeline.Range.Pan(notches * PanPerNotch);
                e.Handled = true;
                break;
        }
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
