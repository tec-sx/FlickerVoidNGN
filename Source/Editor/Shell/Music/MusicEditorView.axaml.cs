using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Controls;

namespace FlickerVoid.Editor.Music;

/// <summary>The music cue editor: the lanes, the track headers beside them and the values of the selected track.</summary>
public partial class MusicEditorView : UserControl
{
    public MusicEditorView()
    {
        InitializeComponent();
        UndoShortcuts.Attach(this, () => History);
    }

    /// <summary>The cue file's undo and redo steps, which Ctrl+Z and Ctrl+Y here step through.</summary>
    public UndoHistory? History { get; set; }

    private void HandleHeaderTapped(object? sender, TappedEventArgs args)
    {
        if (sender is Control { DataContext: MusicTrackViewModel track } && DataContext is MusicEditorViewModel editor)
        {
            editor.Selected = track;
        }
    }
}
