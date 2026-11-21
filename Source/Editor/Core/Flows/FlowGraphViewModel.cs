using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Editor.Core.Inspector;
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
