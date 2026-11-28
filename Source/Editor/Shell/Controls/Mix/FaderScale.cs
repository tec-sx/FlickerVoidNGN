using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlickerVoid.Editor.Controls.Mix;

/// <summary>The marks of a fader's scale (+6, 0, -6, -12, -24) in 9 px hint text, each at its value's height beside the fader.</summary>
public sealed class FaderScale : Control
{
    private static readonly double[] Marks = [6, 0, -6, -12, -24];

    public override void Render(DrawingContext context)
    {
        IBrush hint = DecibelScale.Brush(this, "EditorHintBrush", Brushes.Gray);
        foreach (double mark in Marks)
        {
            double y = Fader.ToY(mark, Bounds.Height);
            string text = DecibelScale.Mark(mark);
            FormattedText label = DecibelScale.Text(text, 9, hint);
            context.DrawText(label, new Point(0, y - (label.Height / 2)));
        }
    }
}
