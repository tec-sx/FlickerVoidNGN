using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Editor.Core.Flows;

/// <summary>A node on the graph canvas: its title, its description of its rules, its place and its pins, laid out in rows.</summary>
public sealed partial class FlowNodeViewModel : ObservableObject
{
    public const double Width = 200;

    public const double HeaderHeight = 24;

    /// <summary>The room for the description between the header and the pins, in nodes that have one.</summary>
    public const double TextHeight = 52;

    public const double RowHeight = 22;

    public FlowNodeViewModel(FlowNode node, string type)
    {
        Node = node;
        Title = DisplayNames.FromIdentifier(type);
        FlowNodeLook look = FlowNodeLook.For(type);
        Kind = look.Kind;
        Icon = look.Icon;
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

    /// <summary>What the node does, as <see cref="FlowNodeLook"/> groups it; tints the header.</summary>
    public string Kind { get; }

    public string Icon { get; }

    /// <summary>The height of the description's room: none for the start and finish, which have nothing to say.</summary>
    public double BodyHeight => Kind is "Start" or "Finish" ? 0 : TextHeight;

    public string Id => Node.Id;

    public double X => Node.X;

    public double Y => Node.Y;

    public double Height => HeaderHeight + BodyHeight + (Math.Max(Inputs.Count, Outputs.Count) * RowHeight) + 6;

    public IReadOnlyList<FlowPinViewModel> Inputs { get; }

    public IReadOnlyList<FlowPinViewModel> Outputs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionLead), nameof(DescriptionText), nameof(DescriptionLines))]
    public partial string Description { get; private set; }

    /// <summary>A dialogue line's speaker, shown in bold over its text; empty for other nodes.</summary>
    public string DescriptionLead => SplitDescription(Kind, Description).Lead;

    /// <summary>How many lines of text fit under the speaker, if there is one.</summary>
    public int DescriptionLines => DescriptionLead.Length > 0 ? 2 : 3;

    public string DescriptionText => SplitDescription(Kind, Description).Text;

    /// <summary>Splits "Speaker: text" for a dialogue node; any other node's description is all text.</summary>
    public static (string Lead, string Text) SplitDescription(string kind, string description)
    {
        int colon = description.IndexOf(": ", StringComparison.Ordinal);
        if (kind != "Dialogue" || colon < 0)
        {
            return ("", description);
        }

        string lead = description[..colon];
        string text = description[(colon + 2)..];
        return (lead, text);
    }

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
