using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Menus;

using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Inspector;

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

    /// <summary>Opens the searchable Add Component menu, the components grouped by module.</summary>
    private void OnAddComponentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control button && DataContext is InspectorViewModel inspector)
        {
            SearchMenu.Show(button, inspector.AddChoices, MenuGrouping.Separators);
        }
    }

    private void HandleRefreshTick(object? sender, EventArgs e)
    {
        if (DataContext is InspectorViewModel inspector)
        {
            inspector.Refresh();
        }
    }
}
