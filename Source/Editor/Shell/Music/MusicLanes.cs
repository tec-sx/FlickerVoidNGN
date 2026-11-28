using System.ComponentModel;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Controls.Mix;
using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Music;

/// <summary>
/// The lanes of an arrangement: the bar ruler with the playhead (click or drag it to jump), a row of the rehearsal for each music
/// area (drag its span to walk the fox in and out), the cues row (double-click to place one, drag to move it), and a lane per
/// layer with its clips drawn as waveforms: fills are hatched, waits dashed, scatter marks dots. A loop clip's right edge drags
/// its length, its top corners drag the fades, sounds dropped on a lane become its clip, and Delete removes what is picked.
/// </summary>
public sealed class MusicLanes : Control
{
    public static readonly StyledProperty<MusicEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<MusicLanes, MusicEditorViewModel?>(nameof(Editor));

    /// <summary>The room left after the last bar.</summary>
    public const double EndGap = 40;

    private const double EdgeGrab = 6;
    private const double Handle = 12;

    private enum Drag { None, Seek, SpanMove, SpanOn, SpanOff, Notifier, LoopSize, FadeIn, FadeOut }

    private Drag _drag;
    private double _grabOffset;
    private RehearsalRow? _row;
    private MusicTrackViewModel? _track;
    private ArrangementClip _clip;
    private NotifierViewModel? _notifier;
    private MusicEditorViewModel? _watched;

    static MusicLanes()
    {
        AffectsRender<MusicLanes>(EditorProperty);
        FocusableProperty.OverrideDefaultValue<MusicLanes>(true);
    }

    public MusicLanes()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    public MusicEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>The top of the rehearsal rows.</summary>
    private static double RowsTop => MusicLayout.RulerHeight;

    private double NotifierTop => RowsTop + ((Editor?.Rows.Count ?? 0) * MusicLayout.RehearsalHeight);

    private double TracksTop => NotifierTop + MusicLayout.NotifierHeight;

    public double ContentWidth => ((Editor?.TotalBars ?? 0) * (Editor?.BarWidth ?? MusicLayout.DefaultBarWidth)) + EndGap;

    public double ContentHeight => TracksTop + (((Editor?.Tracks.Count ?? 0) + 1) * MusicLayout.TrackHeight);

    protected override Size MeasureOverride(Size availableSize) => new(ContentWidth, Math.Max(ContentHeight, availableSize.Height is double.PositiveInfinity ? 0 : availableSize.Height));

    private IBrush Brush(string key) => DecibelScale.Brush(this, key, Brushes.Gray);

    private static FormattedText Label(string text, IBrush brush, double size = 11)
    {
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);
    }

    public override void Render(DrawingContext context)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        double bar = editor.BarWidth;
        double width = Bounds.Width;
        double barsEnd = editor.TotalBars * bar;
        IBrush line = Brush("EditorLineBrush");
        IBrush text = Brush("EditorMutedTextBrush");
        Pen linePen = new(line, 1);
        context.FillRectangle(Brush("EditorDeepBrush"), new Rect(0, 0, width, Bounds.Height));
        DrawGrid(context, editor, bar, barsEnd, linePen, text);
        DrawRows(context, editor, linePen);
        DrawNotifiers(context, editor, linePen);
        DrawSections(context, editor);
        double top = TracksTop;
        context.DrawLine(linePen, new Point(0, top), new Point(width, top));
        for (int index = 0; index < editor.Tracks.Count; index++)
        {
            DrawTrack(context, editor, editor.Tracks[index], LayerTint.Find(index), top, bar);
            top += MusicLayout.TrackHeight;
            context.DrawLine(linePen, new Point(0, top), new Point(width, top));
        }

        DrawPlayhead(context, editor, bar);
    }

    private void DrawGrid(DrawingContext context, MusicEditorViewModel editor, double bar, double barsEnd, Pen linePen, IBrush text)
    {
        Pen faint = new(Brush("EditorPanelBrush"), 1);
        int phrase = editor.IsHours ? 6 : Math.Max(1, (int)editor.PhraseBars);
        for (int index = 0; index <= editor.TotalBars; index++)
        {
            double x = index * bar;
            bool major = index % phrase == 0;
            Pen pen = major ? linePen : faint;
            context.DrawLine(pen, new Point(x, major ? 0 : MusicLayout.RulerHeight), new Point(x, Bounds.Height));
            if (index < editor.TotalBars && (bar >= 18 || major))
            {
                IBrush brush = major ? Brush("EditorTextBrush") : text;
                string number = editor.IsHours ? $"{index:00}:00" : (index + 1).ToString(CultureInfo.InvariantCulture);
                if (!editor.IsHours || index % 3 == 0)
                {
                    context.DrawText(Label(number, brush), new Point(x + 4, 5));
                }
            }
        }

        context.DrawLine(linePen, new Point(0, MusicLayout.RulerHeight), new Point(Bounds.Width, MusicLayout.RulerHeight));
        context.FillRectangle(Brush("EditorFrameBrush"), new Rect(barsEnd, 0, Math.Max(0, Bounds.Width - barsEnd), Bounds.Height));
    }

    private void DrawRows(DrawingContext context, MusicEditorViewModel editor, Pen linePen)
    {
        double top = RowsTop;
        foreach (RehearsalRow row in editor.Rows)
        {
            RehearsalSpan span = row.Span;
            Rect rect = new(span.On * editor.BarWidth, top + 3, (span.Off - span.On) * editor.BarWidth, MusicLayout.RehearsalHeight - 6);
            IBrush fill = row.Asking ? Brush("EditorAccentSoftBrush") : Brush("EditorFillBrush");
            context.DrawRectangle(fill, new Pen(Brush("EditorAccentLineBrush"), 1), rect, 3, 3);
            using (context.PushClip(rect))
            {
                context.DrawText(Label(row.Hint, Brush("EditorTextBrush")), new Point(rect.X + 6, rect.Y + 2));
            }

            top += MusicLayout.RehearsalHeight;
            context.DrawLine(linePen, new Point(0, top), new Point(Bounds.Width, top));
        }
    }

    private void DrawSections(DrawingContext context, MusicEditorViewModel editor)
    {
        foreach (SectionBand band in editor.SectionBands)
        {
            double x = band.Start * editor.BarWidth;
            double width = band.Length * editor.BarWidth;
            Color tint = LayerTint.Find(band.Index);
            context.FillRectangle(new SolidColorBrush(tint), new Rect(x + 1, 0, Math.Max(0, width - 2), 4));
            if (editor.BarWidth >= 18)
            {
                context.DrawText(Label(band.Name, Brush("EditorHintBrush"), 9), new Point(x + 26, 12));
            }
        }

        IBrush accent = Brush("EditorAccentBrush");
        foreach (ContinuePointViewModel point in editor.ContinuePoints)
        {
            double x = (point.Point.Bar - 1) * editor.BarWidth;
            StreamGeometry flag = new();
            using (StreamGeometryContext shape = flag.Open())
            {
                shape.BeginFigure(new Point(x - 4, MusicLayout.RulerHeight), true);
                shape.LineTo(new Point(x + 4, MusicLayout.RulerHeight));
                shape.LineTo(new Point(x, MusicLayout.RulerHeight - 7));
                shape.EndFigure(true);
            }

            context.DrawGeometry(accent, null, flag);
        }
    }

    /// <summary>The bars, from 0, a cue stands on: its own, or that bar of every phrase when it repeats.</summary>
    private static IEnumerable<double> CueBars(MusicEditorViewModel editor, NotifierViewModel cue)
    {
        double first = cue.Notify.Bar - 1;
        yield return first;
        if (!cue.Notify.Repeat)
        {
            yield break;
        }

        int phrase = Math.Max(1, (int)editor.PhraseBars);
        for (double at = first + phrase; at < editor.TotalBars; at += phrase)
        {
            yield return at;
        }
    }

    private void DrawNotifiers(DrawingContext context, MusicEditorViewModel editor, Pen linePen)
    {
        double top = NotifierTop;
        double middle = top + (MusicLayout.NotifierHeight / 2);
        IBrush accent = Brush("EditorAccentBrush");
        IBrush faint = Brush("EditorHintBrush");
        Pen picked = new(Brush("EditorTextBrush"), 1.5);
        foreach (NotifierViewModel notifier in editor.Notifiers)
        {
            bool first = true;
            foreach (double at in CueBars(editor, notifier))
            {
                double x = at * editor.BarWidth;
                StreamGeometry diamond = new();
                using (StreamGeometryContext shape = diamond.Open())
                {
                    shape.BeginFigure(new Point(x, middle - 6), true);
                    shape.LineTo(new Point(x + 5, middle));
                    shape.LineTo(new Point(x, middle + 6));
                    shape.LineTo(new Point(x - 5, middle));
                    shape.EndFigure(true);
                }

                Pen? outline = first && notifier == editor.SelectedCue ? picked : null;
                context.DrawGeometry(first ? accent : faint, outline, diamond);
                if (first)
                {
                    context.DrawText(Label(notifier.Name, Brush("EditorTextBrush"), 10), new Point(x + 8, top + 4));
                }

                first = false;
            }
        }

        context.DrawLine(linePen, new Point(0, top + MusicLayout.NotifierHeight), new Point(Bounds.Width, top + MusicLayout.NotifierHeight));
    }

    private void DrawPlayhead(DrawingContext context, MusicEditorViewModel editor, double bar)
    {
        double x = editor.Playhead * bar;
        IBrush accent = Brush("EditorAccentBrush");
        context.DrawLine(new Pen(accent, 1.5), new Point(x, 0), new Point(x, Bounds.Height));
        FormattedText label = Label(editor.PositionText, Brush("EditorAccentInkBrush"), 10.5);
        Rect chip = new(x - 2, 3, label.Width + 10, 17);
        context.DrawRectangle(accent, null, chip, 3, 3);
        context.DrawText(label, new Point(chip.X + 5, chip.Y + 2));
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

        double mute = track.Muted ? 0.35 : 1;
        using (context.PushOpacity(mute))
        {
            foreach (ArrangementClip clip in track.Clips)
            {
                DrawClip(context, editor, track, clip, tint, y, height, bar);
            }
        }

        if (selected)
        {
            DrawHandles(context, track, y, bar);
        }
    }

    private void DrawClip(DrawingContext context, MusicEditorViewModel editor, MusicTrackViewModel track, ArrangementClip clip, Color tint, double y, double height, double bar)
    {
        double x = clip.Start * bar;
        if (clip.Kind == ArrangementKind.Scatter)
        {
            context.DrawEllipse(new SolidColorBrush(tint), null, new Point(x, y + (height / 2)), 4, 4);
            return;
        }

        Rect rect = new(x, y, Math.Max(2, (clip.Length * bar) - 1), height);
        if (clip.Kind == ArrangementKind.Wait)
        {
            Pen dashed = new(Brush("EditorHintBrush"), 1, new DashStyle([3, 3], 0));
            context.DrawRectangle(null, dashed, rect.Deflate(0.5));
            if (rect.Width > 34)
            {
                context.DrawText(Label("waits", Brush("EditorHintBrush"), 10), new Point(rect.X + 4, rect.Bottom - 15));
            }

            return;
        }

        bool fill = clip.Kind != ArrangementKind.Loop;
        Color body = Color.FromArgb(fill ? (byte)0x88 : (byte)0x55, tint.R, tint.G, tint.B);
        bool picked = editor.SelectedClip is { } chosen && chosen.Track == track && chosen.Clip == clip;
        Pen outline = picked ? new Pen(Brush("EditorTextBrush"), 2) : new Pen(new SolidColorBrush(tint), 1);
        context.DrawRectangle(new SolidColorBrush(body), outline, rect, 3, 3);
        DrawWave(context, editor, track, clip, rect, tint);
        if (fill)
        {
            DrawHatch(context, rect, tint);
        }

        if (clip.Kind == ArrangementKind.Loop)
        {
            DrawFades(context, editor, track, clip, rect);
        }

        DrawClipName(context, editor, track, clip, rect);
    }

    private static void DrawHatch(DrawingContext context, Rect rect, Color tint)
    {
        Pen pen = new(new SolidColorBrush(Color.FromArgb(0x55, tint.R, tint.G, tint.B)), 1);
        using (context.PushClip(rect))
        {
            for (double offset = -rect.Height; offset < rect.Width; offset += 7)
            {
                context.DrawLine(pen, new Point(rect.X + offset, rect.Bottom), new Point(rect.X + offset + rect.Height, rect.Y));
            }
        }
    }

    private void DrawFades(DrawingContext context, MusicEditorViewModel editor, MusicTrackViewModel track, ArrangementClip clip, Rect rect)
    {
        Pen pen = new(Brush("EditorTextBrush"), 1);
        bool run = IsRunStart(track, clip);
        if (run && track.Track.FadeIn > 0)
        {
            double wide = Math.Min(rect.Width, track.Track.FadeIn / editor.BarSeconds * editor.BarWidth);
            context.DrawLine(pen, new Point(rect.X, rect.Bottom), new Point(rect.X + wide, rect.Y));
        }

        if (IsRunEnd(track, clip) && track.Track.FadeOut > 0)
        {
            double wide = Math.Min(rect.Width, track.Track.FadeOut / editor.BarSeconds * editor.BarWidth);
            context.DrawLine(pen, new Point(rect.Right - wide, rect.Y), new Point(rect.Right, rect.Bottom));
        }
    }

    private static bool IsRunStart(MusicTrackViewModel track, ArrangementClip clip)
    {
        return !track.Clips.Any(other => other.Kind == ArrangementKind.Loop && Math.Abs(other.Start + other.Length - clip.Start) < 0.001);
    }

    private static bool IsRunEnd(MusicTrackViewModel track, ArrangementClip clip)
    {
        return !track.Clips.Any(other => other.Kind == ArrangementKind.Loop && Math.Abs(other.Start - (clip.Start + clip.Length)) < 0.001);
    }

    private void DrawHandles(DrawingContext context, MusicTrackViewModel track, double y, double bar)
    {
        IBrush brush = Brush("EditorTextBrush");
        foreach (ArrangementClip clip in track.Clips.Where(clip => clip.Kind == ArrangementKind.Loop))
        {
            double left = clip.Start * bar;
            double right = (clip.Start + clip.Length) * bar;
            if (IsRunStart(track, clip))
            {
                context.DrawRectangle(brush, null, new Rect(left + 1, y + 1, 7, 7), 1, 1);
            }

            if (IsRunEnd(track, clip))
            {
                context.DrawRectangle(brush, null, new Rect(right - 9, y + 1, 7, 7), 1, 1);
            }
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

    private void DrawClipName(DrawingContext context, MusicEditorViewModel editor, MusicTrackViewModel track, ArrangementClip clip, Rect rect)
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

                string length = editor.IsHours ? "seamless loop" : $"{track.Lengths.LoopBars:0} bars";
                name = $"{System.IO.Path.GetFileNameWithoutExtension(track.Track.Loop)} · {length}";
                if (track.IsStretched)
                {
                    name += $" · {track.Track.Tempo:0.#} → {editor.Tempo:0}";
                }

                break;
        }

        using (context.PushClip(rect))
        {
            context.DrawText(Label(name, Brush("EditorTextBrush")), new Point(rect.X + 14, rect.Y + 3));
        }
    }

    private double BarAt(double x) => Math.Max(0, x / (Editor?.BarWidth ?? MusicLayout.DefaultBarWidth));

    protected override void OnPointerPressed(PointerPressedEventArgs args)
    {
        base.OnPointerPressed(args);
        Focus();
        if (Editor is not { } editor)
        {
            return;
        }

        Point point = args.GetPosition(this);
        _drag = Drag.None;
        if (point.Y < RowsTop)
        {
            _drag = Drag.Seek;
            editor.SeekTo(BarAt(point.X));
        }
        else if (point.Y < NotifierTop)
        {
            PressRow(editor, point);
        }
        else if (point.Y < TracksTop)
        {
            PressNotifierRow(editor, point, args.ClickCount);
        }
        else
        {
            PressTracks(editor, point);
        }

        if (_drag != Drag.None)
        {
            args.Pointer.Capture(this);
        }

        InvalidateVisual();
    }

    private void PressRow(MusicEditorViewModel editor, Point point)
    {
        int index = (int)((point.Y - RowsTop) / MusicLayout.RehearsalHeight);
        if (index < 0 || index >= editor.Rows.Count)
        {
            return;
        }

        _row = editor.Rows[index];
        RehearsalSpan span = _row.Span;
        double on = span.On * editor.BarWidth;
        double off = span.Off * editor.BarWidth;
        if (Math.Abs(point.X - on) <= EdgeGrab)
        {
            _drag = Drag.SpanOn;
        }
        else if (Math.Abs(point.X - off) <= EdgeGrab)
        {
            _drag = Drag.SpanOff;
        }
        else if (point.X > on && point.X < off)
        {
            _drag = Drag.SpanMove;
            _grabOffset = BarAt(point.X) - span.On;
        }
    }

    private void PressNotifierRow(MusicEditorViewModel editor, Point point, int clicks)
    {
        foreach (NotifierViewModel notifier in editor.Notifiers)
        {
            foreach (double at in CueBars(editor, notifier))
            {
                if (Math.Abs((at * editor.BarWidth) - point.X) <= 7)
                {
                    _notifier = notifier;
                    _drag = Drag.Notifier;
                    editor.SelectCue(notifier);
                    return;
                }
            }
        }

        if (clicks == 2)
        {
            editor.AddNotifierAt(BarAt(point.X));
        }
        else
        {
            editor.SelectCue(null);
        }
    }

    private void PressTracks(MusicEditorViewModel editor, Point point)
    {
        int index = (int)((point.Y - TracksTop) / MusicLayout.TrackHeight);
        if (index < 0 || index >= editor.Tracks.Count)
        {
            return;
        }

        MusicTrackViewModel track = editor.Tracks[index];
        double y = TracksTop + (index * MusicLayout.TrackHeight) + 4;
        double bar = editor.BarWidth;
        foreach (ArrangementClip clip in track.Clips)
        {
            if (clip.Kind is ArrangementKind.Scatter or ArrangementKind.Wait)
            {
                continue;
            }

            double left = clip.Start * bar;
            double right = (clip.Start + clip.Length) * bar;
            if (point.X < left - EdgeGrab || point.X > right + EdgeGrab)
            {
                continue;
            }

            if (clip.Kind == ArrangementKind.Loop && editor.Selected == track && point.Y < y + Handle)
            {
                if (point.X <= left + Handle && IsRunStart(track, clip))
                {
                    Begin(Drag.FadeIn, track, clip);
                    return;
                }

                if (point.X >= right - Handle && IsRunEnd(track, clip))
                {
                    Begin(Drag.FadeOut, track, clip);
                    return;
                }
            }

            if (clip.Kind == ArrangementKind.Loop && Math.Abs(point.X - right) <= EdgeGrab)
            {
                Begin(Drag.LoopSize, track, clip);
                return;
            }

            if (point.X >= left && point.X <= right)
            {
                editor.SelectClip(track, clip);
                return;
            }
        }

        editor.SelectLayer(track);
    }

    private void Begin(Drag drag, MusicTrackViewModel track, ArrangementClip clip)
    {
        _drag = drag;
        _track = track;
        _clip = clip;
        Editor!.SelectClip(track, clip);
    }

    protected override void OnPointerMoved(PointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (Editor is not { } editor)
        {
            return;
        }

        Point point = args.GetPosition(this);
        if (_drag == Drag.None)
        {
            UpdateCursor(editor, point);
            return;
        }

        double bars = BarAt(point.X);
        switch (_drag)
        {
            case Drag.Seek:
                editor.SeekTo(bars);
                break;
            case Drag.SpanOn:
            case Drag.SpanOff:
            case Drag.SpanMove:
                MoveSpan(editor, bars);
                break;
            case Drag.Notifier:
                double phrase = Math.Max(1, (double)editor.PhraseBars);
                double at = _notifier!.Notify.Repeat ? bars % phrase : bars;
                editor.MoveNotifier(_notifier, at);
                break;
            case Drag.LoopSize:
                editor.SetLoopBars(_track!, (int)Math.Max(1, Math.Round(bars - FirstLoopStart())));
                break;
            case Drag.FadeIn:
                editor.SetFade(_track!, true, bars - _clip.Start);
                break;
            case Drag.FadeOut:
                editor.SetFade(_track!, false, _clip.Start + _clip.Length - bars);
                break;
        }

        InvalidateVisual();
    }

    private double FirstLoopStart()
    {
        double start = _clip.Start;
        foreach (ArrangementClip clip in _track!.Clips)
        {
            if (clip.Kind == ArrangementKind.Loop && clip.Start <= _clip.Start && _clip.Start - clip.Start <= (_clip.Repeat * Math.Max(1, _track.Lengths.LoopBars)) + 0.001)
            {
                start = Math.Min(start, clip.Start);
            }
        }

        return start;
    }

    private void MoveSpan(MusicEditorViewModel editor, double bars)
    {
        if (_row is null)
        {
            return;
        }

        RehearsalSpan span = _row.Span;
        double snapped = Math.Round(bars * 4) / 4;
        snapped = Math.Clamp(snapped, 0, editor.TotalBars);
        RehearsalSpan moved = span;
        switch (_drag)
        {
            case Drag.SpanOn:
                moved = new RehearsalSpan(Math.Min(snapped, span.Off - 0.25), span.Off);
                break;
            case Drag.SpanOff:
                moved = new RehearsalSpan(span.On, Math.Max(snapped, span.On + 0.25));
                break;
            case Drag.SpanMove:
                double length = span.Off - span.On;
                double on = Math.Clamp(Math.Round((bars - _grabOffset) * 4) / 4, 0, editor.TotalBars - length);
                moved = new RehearsalSpan(on, on + length);
                break;
        }

        editor.SetSpan(_row, moved);
    }

    private void UpdateCursor(MusicEditorViewModel editor, Point point)
    {
        Cursor = Cursor.Default;
        if (point.Y < RowsTop)
        {
            Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        if (point.Y >= TracksTop)
        {
            int index = (int)((point.Y - TracksTop) / MusicLayout.TrackHeight);
            if (index >= 0 && index < editor.Tracks.Count)
            {
                foreach (ArrangementClip clip in editor.Tracks[index].Clips.Where(clip => clip.Kind == ArrangementKind.Loop))
                {
                    if (Math.Abs(point.X - ((clip.Start + clip.Length) * editor.BarWidth)) <= EdgeGrab)
                    {
                        Cursor = new Cursor(StandardCursorType.SizeWestEast);
                        return;
                    }
                }
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs args)
    {
        base.OnPointerReleased(args);
        _drag = Drag.None;
        args.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Key == Key.Delete && Editor is { } editor)
        {
            editor.DeleteSelectionCommand.Execute(null);
            _notifier = null;
            args.Handled = true;
        }
    }

    private void HandleDragOver(object? sender, DragEventArgs args)
    {
        bool sound = SoundDrag.GetPath(args.DataTransfer) is not null || SoundDrag.GetFiles(args.DataTransfer).Count > 0;
        args.DragEffects = sound ? DragDropEffects.Copy : DragDropEffects.None;
        args.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs args)
    {
        if (Editor is not { } editor)
        {
            return;
        }

        Point point = args.GetPosition(this);
        int row = (int)Math.Floor((point.Y - TracksTop) / MusicLayout.TrackHeight);
        string? path = SoundDrag.GetPath(args.DataTransfer);
        if (path is not null)
        {
            editor.DropSound(path, row);
        }
        else
        {
            IReadOnlyList<string> files = SoundDrag.GetFiles(args.DataTransfer);
            if (files.Count > 0)
            {
                editor.ImportAndDrop(files, row);
            }
        }

        args.Handled = true;
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
            _watched.Tracks.CollectionChanged += (_, _) => Remeasure();
            _watched.Rows.CollectionChanged += (_, _) => Remeasure();
            _watched.Notifiers.CollectionChanged += (_, _) => Remeasure();
        }
    }

    private void Remeasure()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void HandleEditorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MusicEditorViewModel.BarWidth) or nameof(MusicEditorViewModel.TotalBars))
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }
}
