using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Core.Flows;

/// <summary>
/// A flow graph on the canvas, edited in place: nodes added by every type registered for <see cref="FlowNode"/>, moved, removed
/// and wired output to input, and the selected node's values in a form; <paramref name="edited"/> follows each change.
/// </summary>
public sealed partial class FlowGraphViewModel : ObservableObject
{
    /// <summary>What the canvas sets rather than the form: the id links name and the node's place.</summary>
    private static readonly string[] CanvasProperties = ["id", "x", "y"];

    private readonly FlowGraph _graph;
    private readonly DataTypes _types;
    private readonly Action _edited;
    private readonly FindContentFiles? _files;
    private readonly Dictionary<string, Type> _choices = [];

    /// <summary>How near a pin, in canvas units, a press or a dropped wire still finds it.</summary>
    public const double PinRadius = 14;

    public FlowGraphViewModel(string title, FlowGraph graph, DataTypes types, Action edited, FindContentFiles? files = null)
    {
        Title = title;
        _graph = graph;
        _types = types;
        _edited = edited;
        _files = files;
        JsonTypeInfo nodeType = types.Options.GetTypeInfo(typeof(FlowNode));
        if (nodeType.PolymorphismOptions is { } polymorphism)
        {
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                string name = derived.TypeDiscriminator?.ToString() ?? derived.DerivedType.Name;
                _choices.Add(name, derived.DerivedType);
            }
        }

        NodeChoices = [.. _choices.Keys];
        foreach (FlowNode node in graph.Nodes)
        {
            FlowNodeViewModel created = CreateNode(node);
            Nodes.Add(created);
        }

        foreach (FlowLink link in graph.Links)
        {
            FlowLinkViewModel? created = CreateLink(link);
            if (created is not null)
            {
                Links.Add(created);
            }
        }
    }

    public string Title { get; }

    /// <summary>The <c>$type</c> of each node type the graph may hold.</summary>
    public IReadOnlyList<string> NodeChoices { get; }

    public ObservableCollection<FlowNodeViewModel> Nodes { get; } = [];

    public ObservableCollection<FlowLinkViewModel> Links { get; } = [];

    [ObservableProperty]
    public partial FlowNodeViewModel? Selected { get; private set; }

    /// <summary>The selected node's values.</summary>
    [ObservableProperty]
    public partial DataObjectViewModel? SelectedForm { get; private set; }

    /// <summary>
    /// The Add menu's choices at a point of the canvas: a node of each registered type, grouped by the module that defines it. With
    /// <paramref name="from"/>, the pin a wire was dragged from, only types with a pin to wire it to, and the new node is wired to it.
    /// </summary>
    public IReadOnlyList<MenuChoice> GetAddChoices(double x, double y, FlowPinViewModel? from = null)
    {
        List<MenuChoice> choices = [];
        foreach ((string type, Type nodeType) in _choices)
        {
            if (from is not null && !HasPinFor(nodeType, from))
            {
                continue;
            }

            string name = DisplayNames.FromIdentifier(type);
            string group = GetGroup(nodeType);
            choices.Add(new MenuChoice(name, () => AddWired(type, x, y, from), group, "Flow"));
        }

        return choices;
    }

    /// <summary>The pin whose dot is within <paramref name="radius"/> of the point, nearest first, so pins are easy to hit.</summary>
    public FlowPinViewModel? FindPin(double x, double y, double radius = PinRadius)
    {
        FlowPinViewModel? nearest = null;
        double best = radius * radius;
        foreach (FlowNodeViewModel node in Nodes)
        {
            foreach (FlowPinViewModel pin in node.Inputs.Concat(node.Outputs))
            {
                double dx = pin.Point.X - x;
                double dy = pin.Point.Y - y;
                double distance = (dx * dx) + (dy * dy);
                if (distance <= best)
                {
                    best = distance;
                    nearest = pin;
                }
            }
        }

        return nearest;
    }

    /// <summary>Adds a node of the type registered as <paramref name="type"/> at the point, named by its type and a free number.</summary>
    public FlowNodeViewModel AddNode(string type, double x, double y)
    {
        Type nodeType = _choices[type];
        JsonTypeInfo typeInfo = _types.Options.GetTypeInfo(nodeType);
        FlowNode node = (FlowNode)typeInfo.CreateObject!();
        node.Id = GetFreeId(type);
        node.X = (float)x;
        node.Y = (float)y;
        _graph.Nodes.Add(node);
        FlowNodeViewModel created = CreateNode(node);
        Nodes.Add(created);
        Select(created);
        _edited();
        return created;
    }

    /// <summary>Removes the node and every wire to or from it.</summary>
    public void RemoveNode(FlowNodeViewModel node)
    {
        foreach (FlowLinkViewModel link in Links.Where(link => link.Touches(node)).ToList())
        {
            RemoveLink(link);
        }

        _graph.Nodes.Remove(node.Node);
        Nodes.Remove(node);
        if (Selected == node)
        {
            Select(null);
        }

        _edited();
    }

    [RelayCommand]
    public void RemoveSelected()
    {
        if (Selected is { } node)
        {
            RemoveNode(node);
        }
    }

    public void MoveNode(FlowNodeViewModel node, double x, double y)
    {
        node.MoveTo(x, y);
        foreach (FlowLinkViewModel link in Links)
        {
            if (link.Touches(node))
            {
                link.Refresh();
            }
        }

        _edited();
    }

    /// <summary>Wires an output pin to an input pin of another node, in either order; an output leads to one input, so its old wire goes.</summary>
    public bool Connect(FlowPinViewModel first, FlowPinViewModel second)
    {
        FlowPinViewModel output = first;
        FlowPinViewModel input = second;
        if (!first.IsOutput)
        {
            output = second;
            input = first;
        }

        if (!output.IsOutput || input.IsOutput || output.Node == input.Node)
        {
            return false;
        }

        foreach (FlowLinkViewModel old in Links.Where(link => link.Output == output).ToList())
        {
            RemoveLink(old);
        }

        FlowLink link = new() { From = output.Node.Id, Output = output.Name, To = input.Node.Id, Input = input.Name };
        _graph.Links.Add(link);
        FlowLinkViewModel created = new(link, output, input);
        Links.Add(created);
        _edited();
        return true;
    }

    /// <summary>Removes every wire to or from the pin.</summary>
    public void Disconnect(FlowPinViewModel pin)
    {
        List<FlowLinkViewModel> removed = Links.Where(link => link.Output == pin || link.Input == pin).ToList();
        foreach (FlowLinkViewModel link in removed)
        {
            RemoveLink(link);
        }

        if (removed.Count > 0)
        {
            _edited();
        }
    }

    public void Select(FlowNodeViewModel? node)
    {
        if (Selected is { } previous)
        {
            previous.IsSelected = false;
        }

        Selected = node;
        if (node is null)
        {
            SelectedForm = null;
            return;
        }

        node.IsSelected = true;
        JsonTypeInfo typeInfo = _types.Options.GetTypeInfo(node.Node.GetType());
        SelectedForm = new DataObjectViewModel(node.Title, node.Node, typeInfo, () => EditSelected(node), _files, CanvasProperties);
    }

    private void AddWired(string type, double x, double y, FlowPinViewModel? from)
    {
        FlowNodeViewModel added = AddNode(type, x, y);
        if (from is null)
        {
            return;
        }

        IReadOnlyList<FlowPinViewModel> pins = from.IsOutput ? added.Inputs : added.Outputs;
        if (pins.Count > 0)
        {
            Connect(from, pins[0]);
        }
    }

    private bool HasPinFor(Type nodeType, FlowPinViewModel from)
    {
        JsonTypeInfo typeInfo = _types.Options.GetTypeInfo(nodeType);
        FlowNode sample = (FlowNode)typeInfo.CreateObject!();
        IReadOnlyList<string> pins = from.IsOutput ? sample.Inputs : sample.Outputs;
        return pins.Count > 0;
    }

    /// <summary>The module that defines a node type, from its namespace: Flow for <c>FlickerVoid.Engine.Flow.Nodes</c>.</summary>
    private static string GetGroup(Type nodeType)
    {
        string space = nodeType.Namespace ?? "";
        string[] parts = space.Split('.');
        int index = 0;
        if (index < parts.Length && parts[index] == "FlickerVoid")
        {
            index++;
        }

        if (index < parts.Length && parts[index] == "Engine")
        {
            index++;
        }

        if (index >= parts.Length)
        {
            return "Nodes";
        }

        return parts[index];
    }

    private void EditSelected(FlowNodeViewModel node)
    {
        node.Refresh();
        _edited();
    }

    private void RemoveLink(FlowLinkViewModel link)
    {
        _graph.Links.Remove(link.Link);
        Links.Remove(link);
    }

    private FlowNodeViewModel CreateNode(FlowNode node)
    {
        string type = _choices.First(pair => pair.Value == node.GetType()).Key;
        return new FlowNodeViewModel(node, type);
    }

    /// <summary>The wire of a link between pins that exist; a link to a missing node or pin isn't drawn but stays in the file.</summary>
    private FlowLinkViewModel? CreateLink(FlowLink link)
    {
        FlowNodeViewModel? from = Nodes.FirstOrDefault(node => node.Id == link.From);
        FlowNodeViewModel? to = Nodes.FirstOrDefault(node => node.Id == link.To);
        FlowPinViewModel? output = from?.FindPin(link.Output, true);
        FlowPinViewModel? input = to?.FindPin(link.Input, false);
        if (output is null || input is null)
        {
            return null;
        }

        return new FlowLinkViewModel(link, output, input);
    }

    private string GetFreeId(string type)
    {
        HashSet<string> taken = [.. _graph.Nodes.Select(node => node.Id)];
        if (!taken.Contains(type))
        {
            return type;
        }

        int number = 2;
        while (taken.Contains($"{type}{number}"))
        {
            number++;
        }

        return $"{type}{number}";
    }
}
