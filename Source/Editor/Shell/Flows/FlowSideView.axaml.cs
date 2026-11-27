using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using FlickerVoid.Editor.Core.Flows;

namespace FlickerVoid.Editor.Flows;

/// <summary>The flow editor's side column: the selected node's values and the list of its nodes.</summary>
public partial class FlowSideView : UserControl
{
    public FlowSideView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>A click in the node list selects that node and brings it into view.</summary>
    private void HandleNodeListSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is FlowGraphViewModel graph && sender is ListBox { SelectedItem: FlowNodeViewModel node } && graph.Selected != node)
        {
            graph.Reveal(node);
        }
    }
}
