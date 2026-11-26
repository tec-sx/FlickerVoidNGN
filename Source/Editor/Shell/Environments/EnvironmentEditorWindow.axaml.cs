using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Environments;

/// <summary>
/// The environment editor: the values at the cursor's hour on the left, the hour ruler, the selected colors' strips and the
/// selected numbers' curves on the right, and the hour, play and speed under them.
/// </summary>
public partial class EnvironmentEditorWindow : Window
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private DateTime _lastTick;

    public EnvironmentEditorWindow()
    {
        InitializeComponent();
        UndoShortcuts.Attach(this, () => History);
        _clock.Tick += HandleTick;
        Opened += HandleOpened;
        Closed += (_, _) => _clock.Stop();
    }

    /// <summary>The environment file's undo and redo steps.</summary>
    public UndoHistory? History { get; set; }

    private EnvironmentEditorViewModel? Editor => DataContext as EnvironmentEditorViewModel;

    /// <summary>Starts with the whole day in view.</summary>
    private void HandleOpened(object? sender, EventArgs e)
    {
        _lastTick = DateTime.UtcNow;
        _clock.Start();
        Dispatcher.UIThread.Post(() => Editor?.Curves.Fit(), DispatcherPriority.Loaded);
    }

    private void HandleTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double seconds = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        Editor?.Advance(seconds);
    }

    private void HandleRemoveCurve(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: EnvironmentValueViewModel value })
        {
            Editor?.RemoveCurve(value);
        }
    }
}
