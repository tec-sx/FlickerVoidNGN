using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Editor.Core.Flows;

/// <summary>A wire on the graph canvas from an output pin to an input pin, following its nodes as they move.</summary>
public sealed class FlowLinkViewModel : ObservableObject
{
    public FlowLinkViewModel(FlowLink link, FlowPinViewModel output, FlowPinViewModel input)
    {
        Link = link;
        Output = output;
        Input = input;
    }

    public FlowLink Link { get; }

    public FlowPinViewModel Output { get; }

    public FlowPinViewModel Input { get; }

    public FlowPoint Start => Output.Point;

    public FlowPoint End => Input.Point;

    public bool Touches(FlowNodeViewModel node) => Output.Node == node || Input.Node == node;

    internal void Refresh()
    {
        OnPropertyChanged(nameof(Start));
        OnPropertyChanged(nameof(End));
    }
}
