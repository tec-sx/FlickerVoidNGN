using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Controls.Timelines;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Music;

/// <summary>
/// The arrangement editor: the transport, the lanes with the layers' headers beside them, and the dock under them; it runs the
/// preview's clock while shown. The wheel answers as on every timeline (<see cref="TimelineInput.Read"/>): over the lanes it zooms the
/// bars about the pointer, with Ctrl it makes the lanes taller or shorter about it, with Shift it pans; over the headers, and with
/// Alt, it scrolls. The headers stay at the left as the lanes scroll sideways.
/// </summary>
public partial class MusicEditorView : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime _last;

    public MusicEditorView()
    {
        InitializeComponent();
        UndoShortcuts.Attach(this, () => History);
        _clock.Tick += HandleTick;
        KeyDown += HandleKeyDown;
        LanesContent.AddHandler(PointerWheelChangedEvent, HandleLanesWheel);
        LanesScroll.ScrollChanged += HandleLanesScrolled;
    }

    /// <summary>The arrangement file's undo and redo steps, which Ctrl+Z and Ctrl+Y here step through.</summary>
    public UndoHistory? History { get; set; }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _last = DateTime.UtcNow;
        _clock.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    private void HandleTick(object? sender, EventArgs args)
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan elapsed = now - _last;
        _last = now;
        if (DataContext is MusicEditorViewModel editor)
        {
            editor.Tick(elapsed);
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Space && DataContext is MusicEditorViewModel editor && args.Source is not TextBox)
        {
            editor.PlayCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>Picks the layer whose header was clicked; the lanes take the keys, so Delete removes it.</summary>
    private void HandleHeaderTapped(object? sender, TappedEventArgs args) => Pick(sender);

    private void HandleHeaderMenu(object? sender, ContextRequestedEventArgs args) => Pick(sender);

    private void HandleMoveLayerUp(object? sender, RoutedEventArgs args) => Pick(sender)?.MoveUpCommand.Execute(null);

    private void HandleMoveLayerDown(object? sender, RoutedEventArgs args) => Pick(sender)?.MoveDownCommand.Execute(null);

    private void HandleRemoveLayer(object? sender, RoutedEventArgs args) => Pick(sender)?.RemoveSelectedCommand.Execute(null);

    private MusicEditorViewModel? Pick(object? sender)
    {
        if (sender is not Control { DataContext: MusicTrackViewModel layer } || DataContext is not MusicEditorViewModel editor)
        {
            return null;
        }

        editor.SelectLayer(layer);
        Lanes.Focus();
        return editor;
    }

    private void HandleLanesWheel(object? sender, PointerWheelEventArgs args)
    {
        if (DataContext is not MusicEditorViewModel editor)
        {
            return;
        }

        TimelineWheel wheel = TimelineInput.Read(args.KeyModifiers, args.Delta);
        bool overLanes = args.Source is Visual source && (source == Lanes || Lanes.IsVisualAncestorOf(source));
        switch (wheel)
        {
            case TimelineWheel.ZoomTime when overLanes:
                ZoomBarsAt(editor, args);
                args.Handled = true;
                break;
            case TimelineWheel.ScaleLanes:
                ScaleLanesAt(editor, args);
                args.Handled = true;
                break;
            case TimelineWheel.Pan:
                double notches = TimelineInput.Notches(args.Delta);
                double x = LanesScroll.Offset.X - (notches * TimelineInput.PanPerNotch);
                LanesScroll.Offset = new Vector(x, LanesScroll.Offset.Y);
                args.Handled = true;
                break;
        }
    }

    /// <summary>Zooms the bars so the one under the pointer stays under it.</summary>
    private void ZoomBarsAt(MusicEditorViewModel editor, PointerWheelEventArgs args)
    {
        double x = args.GetPosition(Lanes).X;
        double seen = args.GetPosition(LanesScroll).X;
        double bars = x / editor.BarWidth;
        editor.ZoomBars(args.Delta.Y);
        LanesScroll.UpdateLayout();
        double offset = Lanes.Bounds.X + (bars * editor.BarWidth) - seen;
        LanesScroll.Offset = new Vector(offset, LanesScroll.Offset.Y);
    }

    /// <summary>Makes the lanes taller or shorter so the one under the pointer stays under it.</summary>
    private void ScaleLanesAt(MusicEditorViewModel editor, PointerWheelEventArgs args)
    {
        double y = args.GetPosition(Lanes).Y;
        double seen = args.GetPosition(LanesScroll).Y;
        double top = Lanes.TracksTop;
        double lanes = Math.Max(0, y - top) / editor.LaneHeight;
        editor.ScaleLanes(args.Delta.Y);
        if (y <= top)
        {
            return;
        }

        LanesScroll.UpdateLayout();
        double offset = Lanes.Bounds.Y + top + (lanes * editor.LaneHeight) - seen;
        LanesScroll.Offset = new Vector(LanesScroll.Offset.X, offset);
    }

    /// <summary>Holds the headers at the left while the lanes scroll sideways; they are drawn over the lanes they cover.</summary>
    private void HandleLanesScrolled(object? sender, ScrollChangedEventArgs args)
    {
        Headers.RenderTransform = new TranslateTransform(LanesScroll.Offset.X, 0);
    }

    /// <summary>Zooms so that every bar shows beside the headers.</summary>
    private void HandleFit(object? sender, RoutedEventArgs args)
    {
        if (DataContext is MusicEditorViewModel editor)
        {
            double width = LanesScroll.Viewport.Width - Lanes.Bounds.X - MusicLanes.EndGap;
            editor.FitTo(width);
        }
    }
}
