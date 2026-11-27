using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Music;

/// <summary>The selected clip's waveform with the cue's beats and bars drawn over it, so a loop that is off the grid shows it.</summary>
public sealed class ClipWave : Control
{
    public static readonly StyledProperty<MusicEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<ClipWave, MusicEditorViewModel?>(nameof(Editor));

    static ClipWave()
    {
        AffectsRender<ClipWave>(EditorProperty);
    }

    public MusicEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        IBrush deep = DecibelScale.Brush(this, "EditorDeepBrush", Brushes.Black);
        context.DrawRectangle(deep, null, new Rect(Bounds.Size), 3, 3);
        if (Editor is not { SelectedClip: { } selected } editor || editor.SelectedInfo is not { } info)
        {
            return;
        }

        double mid = Bounds.Height / 2;
        IBrush wave = DecibelScale.Brush(this, "EditorAccentBrush", Brushes.Orange);
        Pen pen = new(wave, 1);
        for (int column = 0; column < (int)Bounds.Width; column += 2)
        {
            int peak = Math.Min(info.Peaks.Length - 1, (int)(column / Bounds.Width * info.Peaks.Length));
            double amount = info.Peaks[peak] * (mid - 4);
            context.DrawLine(pen, new Point(column, mid - amount), new Point(column, mid + amount));
        }

        double tempo = selected.Track.Track.Tempo > 0 ? selected.Track.Track.Tempo : editor.Tempo > 0 ? (double)editor.Tempo : 120;
        double beatSeconds = 60 / tempo;
        int perBar = (int)editor.BeatsPerBar;
        IBrush line = DecibelScale.Brush(this, "EditorLineBrush", Brushes.Gray);
        IBrush strong = DecibelScale.Brush(this, "EditorMutedTextBrush", Brushes.LightGray);
        int beat = 0;
        for (double at = 0; at < info.Seconds; at += beatSeconds, beat++)
        {
            double x = at / info.Seconds * Bounds.Width;
            bool bar = beat % perBar == 0;
            context.DrawLine(new Pen(bar ? strong : line, 1), new Point(x, bar ? 0 : mid - 10), new Point(x, bar ? Bounds.Height : mid + 10));
        }
    }
}
