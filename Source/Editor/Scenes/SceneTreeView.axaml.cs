using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace FlickerVoid.Engine.Editor.Scenes;

/// <summary>Lists the open scene's entities and refreshes twice a second, so renames in the inspector appear.</summary>
public sealed partial class SceneTreeView : UserControl
{
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public SceneTreeView()
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
        if (DataContext is SceneTreeViewModel tree)
        {
            tree.Refresh();
        }
    }
}
