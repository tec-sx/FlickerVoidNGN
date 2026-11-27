using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Music;

/// <summary>The music cue editor: the transport, the lanes with the track headers beside them, and the dock under them; it runs the preview's clock while shown.</summary>
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

    /// <summary>The cue file's undo and redo steps, which Ctrl+Z and Ctrl+Y here step through.</summary>
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

    private void HandleHeaderTapped(object? sender, TappedEventArgs args)
    {
        if (sender is Control { DataContext: MusicTrackViewModel track } && DataContext is MusicEditorViewModel editor)
        {
            editor.Selected = track;
        }
    }
}
