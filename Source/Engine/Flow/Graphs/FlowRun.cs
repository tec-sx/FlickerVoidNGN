using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// One run of a flow graph, like a Flow plugin asset instance: it runs the nodes without inputs as it starts, passes signals along
/// the links, keeps latent nodes active and tells them when facts change, and ends when a finish node is reached or it is stopped.
/// </summary>
public sealed class FlowRun : IDisposable
{
    /// <summary>The most signals one trigger may pass on before the run is stopped as a loop.</summary>
    public const int MaxStepsPerTrigger = 10_000;

    private readonly Dictionary<FlowNode, List<FlowWire>[]> _wires = [];
    private readonly List<FlowNode> _active = [];
    private int _depth;
    private int _steps;

    /// <summary>Resolves the graph's links to its nodes' pins; throws <see cref="InvalidDataException"/> for a link to a missing node or pin.</summary>
    public FlowRun(FlowGraph graph, ConditionContext conditions)
    {
        Graph = graph;
        Conditions = conditions;
        Dictionary<string, FlowNode> nodes = [];
        foreach (FlowNode node in graph.Nodes)
        {
            if (!nodes.TryAdd(node.Id, node))
            {
                throw new InvalidDataException($"Two nodes have the id {node.Id}.");
            }

            List<FlowWire>[] outputs = new List<FlowWire>[node.Outputs.Count];
            for (int index = 0; index < outputs.Length; index++)
            {
                outputs[index] = [];
            }

            _wires.Add(node, outputs);
        }

        foreach (FlowLink link in graph.Links)
        {
            FlowNode from = FindNode(nodes, link.From, link);
            FlowNode to = FindNode(nodes, link.To, link);
            int output = FindPin(from.Outputs, link.Output, from, "output");
            int input = FindPin(to.Inputs, link.Input, to, "input");
            FlowWire wire = new(to, input);
            _wires[from][output].Add(wire);
        }
    }

    /// <summary>Raised once when the run ends, by a finish node or <see cref="Finish"/>.</summary>
    public event Action<FlowRun>? Finished;

    public FlowGraph Graph { get; }

    public ConditionContext Conditions { get; }

    public bool IsRunning { get; private set; }

    public IReadOnlyList<FlowNode> ActiveNodes => _active;

    /// <summary>Starts the run: every node without inputs runs, in the graph's order.</summary>
    public void Start()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("The flow run has already started.");
        }

        IsRunning = true;
        Conditions.Facts.FactChanged += HandleFactChanged;
        foreach (FlowNode node in Graph.Nodes.ToList())
        {
            if (!IsRunning)
            {
                break;
            }

            if (node.Inputs.Count == 0)
            {
                node.Execute(this, -1);
            }
        }
    }

    /// <summary>Ends the run: active nodes clean up and <see cref="Finished"/> is raised.</summary>
    public void Finish()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        Conditions.Facts.FactChanged -= HandleFactChanged;
        foreach (FlowNode node in _active.ToList())
        {
            node.Cleanup(this);
        }

        _active.Clear();
        Finished?.Invoke(this);
    }

    public void Dispose() => Finish();

    internal void Activate(FlowNode node)
    {
        if (!_active.Contains(node))
        {
            _active.Add(node);
        }
    }

    internal void Deactivate(FlowNode node)
    {
        if (_active.Remove(node))
        {
            node.Cleanup(this);
        }
    }

    /// <summary>Passes a signal from the node's output to every input wired to it, in the links' order.</summary>
    internal void Trigger(FlowNode node, int output)
    {
        if (!IsRunning)
        {
            return;
        }

        if (_depth == 0)
        {
            _steps = 0;
        }

        _depth++;
        try
        {
            foreach (FlowWire wire in _wires[node][output].ToArray())
            {
                if (!IsRunning)
                {
                    return;
                }

                _steps++;
                if (_steps > MaxStepsPerTrigger)
                {
                    Finish();
                    throw new InvalidOperationException($"The flow passed more than {MaxStepsPerTrigger} signals from {node.Id} without stopping; its links loop.");
                }

                wire.Target.Execute(this, wire.Input);
            }
        }
        finally
        {
            _depth--;
        }
    }

    private static FlowNode FindNode(Dictionary<string, FlowNode> nodes, string id, FlowLink link)
    {
        if (!nodes.TryGetValue(id, out FlowNode? node))
        {
            throw new InvalidDataException($"The link from {link.From} to {link.To} names the missing node {id}.");
        }

        return node;
    }

    private static int FindPin(IReadOnlyList<string> pins, string name, FlowNode node, string kind)
    {
        for (int index = 0; index < pins.Count; index++)
        {
            if (pins[index] == name)
            {
                return index;
            }
        }

        throw new InvalidDataException($"The node {node.Id} has no {kind} {name}.");
    }

    private void HandleFactChanged(Tag tag, int oldValue, int newValue)
    {
        foreach (FlowNode node in _active.ToList())
        {
            if (!IsRunning)
            {
                return;
            }

            if (_active.Contains(node))
            {
                node.OnFactChanged(this);
            }
        }
    }

    /// <summary>Where a signal goes from an output: a node and the index of its input.</summary>
    private readonly record struct FlowWire(FlowNode Target, int Input);
}
