using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Editor.Core.Flows;

/// <summary>A node on the graph canvas: its title, its description of its rules, its place and its pins, laid out in rows.</summary>
public sealed partial class FlowNodeViewModel : ObservableObject
{
    public const double Width = 180;

    public const double HeaderHeight = 40;

    public const double RowHeight = 22;

    public FlowNodeViewModel(FlowNode node, string type)
    {
        Node = node;
        Title = DisplayNames.FromIdentifier(type);
        List<FlowPinViewModel> inputs = [];
        for (int index = 0; index < node.Inputs.Count; index++)
        {
            inputs.Add(new FlowPinViewModel(this, node.Inputs[index], index, false));
        }

        List<FlowPinViewModel> outputs = [];
        for (int index = 0; index < node.Outputs.Count; index++)
        {
            outputs.Add(new FlowPinViewModel(this, node.Outputs[index], index, true));
        }

        Inputs = inputs;
        Outputs = outputs;
        Description = node.Describe();
    }

    public FlowNode Node { get; }

    public string Title { get; }

    public string Id => Node.Id;

    public double X => Node.X;

    public double Y => Node.Y;

    public double Height => HeaderHeight + (Math.Max(Inputs.Count, Outputs.Count) * RowHeight) + 6;

    public IReadOnlyList<FlowPinViewModel> Inputs { get; }

    public IReadOnlyList<FlowPinViewModel> Outputs { get; }

    [ObservableProperty]
    public partial string Description { get; private set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public FlowPinViewModel? FindPin(string name, bool output)
    {
        IReadOnlyList<FlowPinViewModel> pins = output ? Outputs : Inputs;
        foreach (FlowPinViewModel pin in pins)
        {
            if (pin.Name == name)
            {
                return pin;
            }
        }

        return null;
    }

    internal void MoveTo(double x, double y)
    {
        Node.X = (float)x;
        Node.Y = (float)y;
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
    }

    internal void Refresh()
    {
        Description = Node.Describe();
    }
}
