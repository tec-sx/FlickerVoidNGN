namespace FlickerVoid.Editor.Core.Flows;

/// <summary>An input or output pin of a node on the graph canvas, placed by the node's layout.</summary>
public sealed class FlowPinViewModel(FlowNodeViewModel node, string name, int index, bool isOutput)
{
    public FlowNodeViewModel Node { get; } = node;

    public string Name { get; } = name;

    public bool IsOutput { get; } = isOutput;

    /// <summary>Where wires meet the pin, in canvas coordinates.</summary>
    public FlowPoint Point
    {
        get
        {
            double y = Node.Y + FlowNodeViewModel.HeaderHeight + ((index + 0.5) * FlowNodeViewModel.RowHeight);
            if (IsOutput)
            {
                return new FlowPoint(Node.X + FlowNodeViewModel.Width, y);
            }

            return new FlowPoint(Node.X, y);
        }
    }
}
