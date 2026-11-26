using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Curves;

/// <summary>
/// Draws a curve editor's tracks over a grid of hours and values and hands it the pointer: a press takes a key or handle
/// (Shift adds to the selection), a drag moves it (Alt without snapping), a double-click adds a key, the middle or right
/// button pans, the wheel zooms (Ctrl the time alone, Shift the value alone), Delete removes the selected keys, F fits, and a
/// right-click without a drag offers the keys' modes.
/// </summary>
public sealed class CurveView : Control
{
    public static readonly StyledProperty<CurveEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<CurveView, CurveEditorViewModel?>(nameof(Editor));

    /// <summary>The time a cursor line is drawn at, such as the hour an environment is shown at; NaN draws none.</summary>
    public static readonly StyledProperty<double> CursorTimeProperty =
        AvaloniaProperty.Register<CurveView, double>(nameof(CursorTime), double.NaN);

    /// <summary>The colors tracks are drawn in, by their place among the editor's tracks.</summary>
    public static readonly IReadOnlyList<Color> TrackColors =
    [
        Color.Parse("#E0AC2C"),
        Color.Parse("#5ACCFF"),
        Color.Parse("#A6C33F"),
        Color.Parse("#D97BA6"),
        Color.Parse("#B48EC9"),
        Color.Parse("#E0664A"),
    ];

    private const double KeySize = 4;
    private const double LabelSize = 10;

    /// <summary>How far the mouse may move, in pixels, for a right-click to open the modes rather than pan.</summary>
    private const double ClickSlop = 4;

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#0B0705"));
    private static readonly IPen MinorPen = new Pen(new SolidColorBrush(Color.Parse("#1D1610")), 1);
    private static readonly IPen MajorPen = new Pen(new SolidColorBrush(Color.Parse("#2A2119")), 1);
    private static readonly IPen DayPen = new Pen(new SolidColorBrush(Color.Parse("#3A2F26")), 1);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#8F8372"));
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#F5A639"));
    private static readonly IBrush KeyBrush = new SolidColorBrush(Color.Parse("#EDE5D8"));
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.Parse("#B6AB99")), 1);
    private static readonly IPen CursorPen = new Pen(new SolidColorBrush(Color.Parse("#D9B23A")), 2);

    private readonly List<CurveTrack> _watchedTracks = [];
    private CurveEditorViewModel? _watched;
    private Point? _panFrom;
    private Point _panStart;
    private bool _pressedKey;

    static CurveView()
    {
        AffectsRender<CurveView>(EditorProperty, CursorTimeProperty);
        FocusableProperty.OverrideDefaultValue<CurveView>(true);
    }

    public CurveView()
    {
        ClipToBounds = true;
        ToolTip.SetTip(this, "Drag keys (Alt: no snapping), double-click to add one, Delete to remove, F to fit, right-click for modes.");
    }

    public CurveEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public double CursorTime
    {
        get => GetValue(CursorTimeProperty);
        set => SetValue(CursorTimeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        Rect bounds = new(Bounds.Size);
        context.DrawRectangle(Background, null, bounds);
        if (Editor is not { } editor)
        {
            return;
        }

        editor.Range.Resize(bounds.Width, bounds.Height);
        DrawTimeLines(context, editor.Range);
        DrawValueLines(context, editor.Range);
        for (int i = 0; i < editor.Tracks.Count; i++)
        {
            CurveTrack track = editor.Tracks[i];
            bool active = ReferenceEquals(track, editor.Active);
            DrawLine(context, editor, track, GetTrackColor(i), active);
        }

        if (editor.Active is { } shown)
        {
            DrawKeys(context, editor, shown);
        }

        if (!double.IsNaN(CursorTime))
        {
            double x = editor.Range.ToScreen(CursorTime, 0).X;
            context.DrawLine(CursorPen, new Point(x, 0), new Point(x, bounds.Height));
        }
    }

    public static Color GetTrackColor(int index) => TrackColors[index % TrackColors.Count];

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Editor?.Range.Resize(e.NewSize.Width, e.NewSize.Height);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != EditorProperty)
        {
            return;
        }

        Unwatch();
        _watched = Editor;
        if (_watched is not null)
        {
            _watched.PropertyChanged += HandleChanged;
            _watched.Range.PropertyChanged += HandleChanged;
            _watched.Tracks.CollectionChanged += HandleTracksChanged;
            WatchTracks();
            _watched.Range.Resize(Bounds.Width, Bounds.Height);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unwatch();
        _watched = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Editor is not { } editor)
        {
            return;
        }

        Focus();
        Point position = e.GetPosition(this);
        PointerPointProperties buttons = e.GetCurrentPoint(this).Properties;
        if (buttons.IsMiddleButtonPressed || buttons.IsRightButtonPressed)
        {
            _panFrom = position;
            _panStart = position;
        }
        else if (buttons.IsLeftButtonPressed && e.ClickCount == 2)
        {
            editor.AddKey(position.X, position.Y);
        }
        else if (buttons.IsLeftButtonPressed)
        {
            bool extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _pressedKey = editor.Press(position.X, position.Y, extend);
        }

        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Editor is not { } editor)
        {
            return;
        }

        Point position = e.GetPosition(this);
        if (_panFrom is { } from)
        {
            editor.Range.Pan(position.X - from.X, position.Y - from.Y);
            _panFrom = position;
        }
        else if (_pressedKey)
        {
            bool snap = !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
            editor.Drag(position.X, position.Y, snap);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Editor is { } editor)
        {
            Point position = e.GetPosition(this);
            bool clicked = _panFrom is not null && Point.Distance(position, _panStart) <= ClickSlop;
            if (clicked && e.InitialPressMouseButton == MouseButton.Right && editor.SelectedKeys.Count > 0)
            {
                ShowModes(editor);
            }

            editor.Release();
        }

        _panFrom = null;
        _pressedKey = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Editor is not { } editor)
        {
            return;
        }

        CurveAxes axes = CurveAxes.Both;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            axes = CurveAxes.Time;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            axes = CurveAxes.Value;
        }

        Point position = e.GetPosition(this);
        editor.Range.ZoomAt(position.X, position.Y, e.Delta.Y, axes);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Editor is not { } editor || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Delete)
        {
            editor.DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F)
        {
            editor.Fit();
            e.Handled = true;
        }
    }

    private void ShowModes(CurveEditorViewModel editor)
    {
        ContextMenu menu = new();
        foreach (CurveMode mode in Enum.GetValues<CurveMode>())
        {
            MenuItem item = new() { Header = mode.ToString() };
            item.Click += (_, _) => editor.SetMode(mode);
            menu.Items.Add(item);
        }

        menu.Open(this);
    }

    private void DrawTimeLines(DrawingContext context, CurveRange range)
    {
        double step = GetStep(range.MaxTime - range.MinTime, Bounds.Width);
        double first = Math.Ceiling(range.MinTime / step) * step;
        for (double time = first; time <= range.MaxTime; time += step)
        {
            double x = range.ToScreen(time, 0).X;
            IPen pen = MinorPen;
            if (IsWhole(time, 24))
            {
                pen = DayPen;
            }
            else if (IsWhole(time, step * 4))
            {
                pen = MajorPen;
            }

            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
            string label = FormatHour(time);
            DrawLabel(context, label, new Point(x + 2, 1));
        }
    }

    private void DrawValueLines(DrawingContext context, CurveRange range)
    {
        double step = GetStep(range.MaxValue - range.MinValue, Bounds.Height);
        double first = Math.Ceiling(range.MinValue / step) * step;
        for (double value = first; value <= range.MaxValue; value += step)
        {
            double y = range.ToScreen(0, value).Y;
            IPen pen = IsWhole(value, step * 4) ? MajorPen : MinorPen;
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
            string label = value.ToString("0.###", CultureInfo.InvariantCulture);
            DrawLabel(context, label, new Point(2, y - LabelSize - 2));
        }
    }

    private void DrawLine(DrawingContext context, CurveEditorViewModel editor, CurveTrack track, Color color, bool active)
    {
        int samples = (int)Math.Max(Bounds.Width / 2, 2);
        IReadOnlyList<CurvePoint> line = editor.GetLine(track, samples);
        StreamGeometry geometry = new();
        using (StreamGeometryContext figure = geometry.Open())
        {
            figure.BeginFigure(ToPoint(line[0]), false);
            for (int i = 1; i < line.Count; i++)
            {
                figure.LineTo(ToPoint(line[i]));
            }

            figure.EndFigure(false);
        }

        double opacity = active ? 1 : 0.45;
        Pen pen = new(new SolidColorBrush(color, opacity), active ? 2 : 1);
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawKeys(DrawingContext context, CurveEditorViewModel editor, CurveTrack track)
    {
        for (int i = 0; i < track.Curve.Keys.Count; i++)
        {
            CurveKey key = track.Curve.Keys[i];
            Point at = ToPoint(editor.Range.ToScreen(key.Time, key.Value));
            if (editor.GetHandles(i) is { } handles)
            {
                Point arrive = ToPoint(handles.In);
                Point leave = ToPoint(handles.Out);
                context.DrawLine(HandlePen, arrive, leave);
                context.DrawEllipse(KeyBrush, null, arrive, 3, 3);
                context.DrawEllipse(KeyBrush, null, leave, 3, 3);
            }

            IBrush fill = editor.SelectedKeys.Contains(i) ? SelectedBrush : KeyBrush;
            Rect box = new(at.X - KeySize, at.Y - KeySize, KeySize * 2, KeySize * 2);
            context.DrawRectangle(fill, null, box);
        }
    }

    private static void DrawLabel(DrawingContext context, string text, Point at)
    {
        FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, LabelSize, LabelBrush);
        context.DrawText(formatted, at);
    }

    /// <summary>A round step that puts grid lines some 40 pixels or more apart.</summary>
    private static double GetStep(double span, double pixels)
    {
        double wanted = span * 40 / Math.Max(pixels, 1);
        double power = Math.Pow(10, Math.Floor(Math.Log10(wanted)));
        foreach (double multiple in (ReadOnlySpan<double>)[1, 2, 2.5, 5, 10])
        {
            if (power * multiple >= wanted)
            {
                return power * multiple;
            }
        }

        return power * 10;
    }

    private static bool IsWhole(double value, double step)
    {
        double ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }

    /// <summary>An hour of the day as a clock shows it, wrapped into the day.</summary>
    private static string FormatHour(double time)
    {
        double hour = ((time % 24) + 24) % 24;
        int minutes = (int)Math.Round(hour * 60);
        return $"{minutes / 60 % 24:00}:{minutes % 60:00}";
    }

    private static Point ToPoint(CurvePoint point) => new(point.X, point.Y);

    private void Unwatch()
    {
        if (_watched is null)
        {
            return;
        }

        _watched.PropertyChanged -= HandleChanged;
        _watched.Range.PropertyChanged -= HandleChanged;
        _watched.Tracks.CollectionChanged -= HandleTracksChanged;
        UnwatchTracks();
    }

    /// <summary>Follows each track's curve, so an edit or a drag redraws the view.</summary>
    private void WatchTracks()
    {
        UnwatchTracks();
        foreach (CurveTrack track in _watched?.Tracks ?? [])
        {
            track.PropertyChanged += HandleChanged;
            _watchedTracks.Add(track);
        }
    }

    private void UnwatchTracks()
    {
        foreach (CurveTrack track in _watchedTracks)
        {
            track.PropertyChanged -= HandleChanged;
        }

        _watchedTracks.Clear();
    }

    private void HandleChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private void HandleTracksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchTracks();
        InvalidateVisual();
    }
}
