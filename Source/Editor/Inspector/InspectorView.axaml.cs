using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>Shows the inspector and refreshes it ten times a second, so gizmo drags and other changes appear in its fields.</summary>
public sealed partial class InspectorView : UserControl
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public InspectorView()
    {
        AvaloniaXamlLoader.Load(this);
        _refresh.Tick += HandleRefreshTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _refresh.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _refresh.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void HandleRefreshTick(object? sender, EventArgs e)
    {
        if (DataContext is InspectorViewModel inspector)
        {
            inspector.Refresh();
        }
    }
}
