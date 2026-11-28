using Avalonia.Controls;

namespace FlickerVoid.Editor.Controls.Timelines;

/// <summary>
/// A row of joined buttons, one chosen (or several with <c>SelectionMode="Multiple,Toggle"</c>), with icons and text (rule R28):
/// the dock and panel tabs (a 26 px bar, the chosen one raised with an accent bar at its foot) and, with the <c>chips</c> class, a
/// row of rounded filters. One control in place of the editor's separate tab and toggle styles.
/// </summary>
public class ButtonGroup : ListBox
{
    public const double TabHeight = 26;

}
