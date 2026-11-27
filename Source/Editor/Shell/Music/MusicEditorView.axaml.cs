using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Music;

/// <summary>The arrangement editor: the transport, the lanes with the layers' headers beside them, and the dock under them; it runs the preview's clock while shown.</summary>
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
