using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Music;

/// <summary>
/// The lanes of a music cue: the bar ruler, the rehearsal's span (drag its body or its edges to move when the game asks for
/// tracks), and a row per track with its clips, drawn with waveforms; waits are dashed, fills hatched and scatter marks dots.
/// </summary>
public sealed class MusicLanes : Control
{
    public static readonly StyledProperty<MusicEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<MusicLanes, MusicEditorViewModel?>(nameof(Editor));

    private const double EdgeGrab = 6;

    private enum Drag { None, Move, On, Off }

    private Drag _drag;
    private double _grabOffset;
    private MusicEditorViewModel? _watched;

    static MusicLanes()
    {
        AffectsRender<MusicLanes>(EditorProperty);
    }

    public MusicEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public double ContentWidth => (Editor?.TotalBars ?? 0) * (Editor?.BarWidth ?? MusicLayout.DefaultBarWidth);

    public double ContentHeight
    {
        get
        {
            int tracks = Editor?.Tracks.Count ?? 0;
            return MusicLayout.RulerHeight + MusicLayout.RehearsalHeight + (tracks * MusicLayout.TrackHeight);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(ContentWidth, ContentHeight);

    private Color Tint(int index)
    {
        string key = $"EditorTrack{(index % 6) + 1}";
        if (this.TryFindResource(key, ActualThemeVariant, out object? value) && value is Color color)
        {
            return color;
        }

        return Colors.Gray;
    }

    private IBrush Brush(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out object? value) && value is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public override void Render(DrawingContext context)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        double bar = editor.BarWidth;
        double width = Bounds.Width;
        IBrush line = Brush("EditorLineBrush");
        IBrush text = Brush("EditorMutedTextBrush");
        Pen linePen = new(line, 1);
        context.FillRectangle(Brush("EditorDeepBrush"), new Rect(0, 0, width, Bounds.Height));

        for (int index = 0; index <= editor.TotalBars; index++)
        {
            double x = index * bar;
            bool major = index % editor.PhraseBars == 0;
            Pen pen = major ? linePen : new Pen(Brush("EditorPanelBrush"), 1);
            context.DrawLine(pen, new Point(x, major ? 0 : MusicLayout.RulerHeight), new Point(x, Bounds.Height));
            if (index < editor.TotalBars)
            {
                FormattedText label = new((index + 1).ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, major ? Brush("EditorTextBrush") : text);
                context.DrawText(label, new Point(x + 4, 5));
            }
        }

        double top = MusicLayout.RulerHeight;
        context.DrawLine(linePen, new Point(0, top), new Point(width, top));
        DrawRehearsal(context, editor, top);
        top += MusicLayout.RehearsalHeight;
        context.DrawLine(linePen, new Point(0, top), new Point(width, top));
        for (int index = 0; index < editor.Tracks.Count; index++)
        {
            DrawTrack(context, editor, editor.Tracks[index], Tint(index), top, bar);
            top += MusicLayout.TrackHeight;
            context.DrawLine(linePen, new Point(0, top), new Point(width, top));
        }
    }

    private void DrawRehearsal(DrawingContext context, MusicEditorViewModel editor, double top)
    {
        if (!editor.HasRehearsal || editor.Spans.Count == 0)
        {
            return;
        }

        RehearsalSpan span = editor.Spans[0];
        Rect rect = new(span.On * editor.BarWidth, top + 3, (span.Off - span.On) * editor.BarWidth, MusicLayout.RehearsalHeight - 6);
        context.DrawRectangle(Brush("EditorFillBrush"), new Pen(Brush("EditorAccentLineBrush"), 1), rect, 3, 3);
        FormattedText label = new("the fox is in the area", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brush("EditorTextBrush"));
        using (context.PushClip(rect))
        {
            context.DrawText(label, new Point(rect.X + 6, rect.Y + 2));
        }
    }

    private void DrawTrack(DrawingContext context, MusicEditorViewModel editor, MusicTrackViewModel track, Color tint, double top, double bar)
    {
        double height = MusicLayout.TrackHeight - 8;
        double y = top + 4;
        bool selected = track == editor.Selected;
        if (selected)
        {
            context.FillRectangle(Brush("EditorRowHoverBrush"), new Rect(0, top, Bounds.Width, MusicLayout.TrackHeight));
        }

        foreach (ArrangementClip clip in track.Clips)
        {
            double x = clip.Start * bar;
            if (clip.Kind == ArrangementKind.Scatter)
            {
                context.DrawEllipse(new SolidColorBrush(tint), null, new Point(x, y + (height / 2)), 4, 4);
                continue;
            }

            Rect rect = new(x, y, Math.Max(2, clip.Length * bar - 1), height);
            if (clip.Kind == ArrangementKind.Wait)
            {
                Pen dashed = new(Brush("EditorHintBrush"), 1, new DashStyle([3, 3], 0));
                context.DrawRectangle(null, dashed, rect.Deflate(0.5));
                continue;
            }

            Color fill = Color.FromArgb(clip.Kind == ArrangementKind.Loop ? (byte)0x55 : (byte)0x88, tint.R, tint.G, tint.B);
            context.DrawRectangle(new SolidColorBrush(fill), new Pen(new SolidColorBrush(tint), 1), rect, 3, 3);
            DrawWave(context, editor, track, clip, rect, tint);
            DrawClipName(context, track, clip, rect);
        }
    }

    private static void DrawWave(DrawingContext context, MusicEditorViewModel editor, MusicTrackViewModel track, ArrangementClip clip, Rect rect, Color tint)
    {
        string file = clip.Kind switch
        {
            ArrangementKind.FillIn => track.Track.FillIn,
            ArrangementKind.FillOut => track.Track.FillOut,
            _ => track.Track.Loop,
        };
        ClipInfo? info = editor.Catalog.Find(file);
        if (info is null || rect.Width < 4)
        {
            return;
        }

        Pen pen = new(new SolidColorBrush(tint), 1);
        double mid = rect.Y + (rect.Height / 2) + 6;
        double half = (rect.Height - 20) / 2;
        int columns = (int)rect.Width;
        double span = clip.Length;
        double lengthBars = clip.Kind == ArrangementKind.Loop ? track.Lengths.LoopBars : span;
        for (int column = 0; column < columns; column += 2)
        {
            double at = column / rect.Width * span / Math.Max(lengthBars, 0.0001);
            at -= Math.Floor(at);
            int peak = Math.Min(info.Peaks.Length - 1, (int)(at * info.Peaks.Length));
            double amount = info.Peaks[peak] * half;
            context.DrawLine(pen, new Point(rect.X + column, mid - amount), new Point(rect.X + column, mid + amount));
        }
    }

    private void DrawClipName(DrawingContext context, MusicTrackViewModel track, ArrangementClip clip, Rect rect)
    {
        string name;
        switch (clip.Kind)
        {
            case ArrangementKind.FillIn:
                name = "Fill-in";
                break;
            case ArrangementKind.FillOut:
                name = "Fill-out";
                break;
            default:
                if (clip.Repeat > 0)
                {
                    return;
                }

                name = System.IO.Path.GetFileNameWithoutExtension(track.Track.Loop);
                break;
        }

        FormattedText label = new(name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brush("EditorTextBrush"));
        using (context.PushClip(rect))
        {
            context.DrawText(label, new Point(rect.X + 5, rect.Y + 3));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        base.OnPointerPressed(args);
        if (Editor is not { } editor)
        {
            return;
        }

        Point point = args.GetPosition(this);
        if (point.Y >= MusicLayout.RulerHeight + MusicLayout.RehearsalHeight)
        {
            int row = (int)((point.Y - MusicLayout.RulerHeight - MusicLayout.RehearsalHeight) / MusicLayout.TrackHeight);
            if (row >= 0 && row < editor.Tracks.Count)
            {
                editor.Selected = editor.Tracks[row];
                InvalidateVisual();
            }

            return;
        }

        if (point.Y < MusicLayout.RulerHeight || !editor.HasRehearsal || editor.Spans.Count == 0)
        {
            return;
        }

        RehearsalSpan span = editor.Spans[0];
        double on = span.On * editor.BarWidth;
        double off = span.Off * editor.BarWidth;
        if (Math.Abs(point.X - on) <= EdgeGrab)
        {
            _drag = Drag.On;
        }
        else if (Math.Abs(point.X - off) <= EdgeGrab)
        {
            _drag = Drag.Off;
        }
        else if (point.X > on && point.X < off)
        {
            _drag = Drag.Move;
            _grabOffset = (point.X / editor.BarWidth) - span.On;
        }

        if (_drag != Drag.None)
        {
            args.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_drag == Drag.None || Editor is not { } editor)
        {
            return;
        }

        RehearsalSpan span = editor.Spans[0];
        double bars = Math.Round(args.GetPosition(this).X / editor.BarWidth * 4) / 4;
        bars = Math.Clamp(bars, 0, editor.TotalBars);
        RehearsalSpan moved = span;
        switch (_drag)
        {
            case Drag.On:
                moved = new RehearsalSpan(Math.Min(bars, span.Off - 0.25), span.Off);
                break;
            case Drag.Off:
                moved = new RehearsalSpan(span.On, Math.Max(bars, span.On + 0.25));
                break;
            case Drag.Move:
                double length = span.Off - span.On;
                double on = Math.Clamp(Math.Round((args.GetPosition(this).X / editor.BarWidth - _grabOffset) * 4) / 4, 0, editor.TotalBars - length);
                moved = new RehearsalSpan(on, on + length);
                break;
        }

        editor.SetSpan(0, moved);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        base.OnPointerReleased(args);
        _drag = Drag.None;
        args.Pointer.Capture(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != EditorProperty)
        {
            return;
        }

        if (_watched is not null)
        {
            _watched.PropertyChanged -= HandleEditorChanged;
        }

        _watched = Editor;
        if (_watched is not null)
        {
            _watched.PropertyChanged += HandleEditorChanged;
            _watched.Tracks.CollectionChanged += (_, _) => HandleEditorChanged(null, null);
        }
    }

    private void HandleEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs? args)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }
}
