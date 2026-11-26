using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Editor.Core.Undo;

namespace FlickerVoid.Editor.Environments;

/// <summary>
/// The Day dock under the viewport: the values at the cursor's hour on the left, the hour ruler, the selected colors' strips and
/// the selected numbers' curves on the right, and the hour, play and speed under them.
/// </summary>
public partial class EnvironmentEditorView : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private DateTime _lastTick;

    public EnvironmentEditorView()
    {
        InitializeComponent();
        UndoShortcuts.Attach(this, () => History);
        _clock.Tick += HandleTick;
        DataContextChanged += (_, _) => FitLater();
    }

    /// <summary>The environment file's undo and redo steps.</summary>
    public UndoHistory? History { get; set; }

    /// <summary>Raised by Close.</summary>
    public event Action? CloseRequested;

    private EnvironmentEditorViewModel? Editor => DataContext as EnvironmentEditorViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lastTick = DateTime.UtcNow;
        _clock.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Starts each environment with the whole day in view.</summary>
    private void FitLater() => Dispatcher.UIThread.Post(() => Editor?.Curves.Fit(), DispatcherPriority.Loaded);

    private void HandleTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        double seconds = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (IsVisible)
        {
            Editor?.Advance(seconds);
        }
    }

    private void HandleClose(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void HandleRemoveCurve(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: EnvironmentValueViewModel value })
        {
            Editor?.RemoveCurve(value);
        }
    }
}
