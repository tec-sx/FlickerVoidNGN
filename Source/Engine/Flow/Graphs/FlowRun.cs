using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// One run of a flow graph, like a Flow plugin asset instance: it runs the nodes without inputs as it starts, passes signals along
/// the links, keeps latent nodes active and tells them when facts change, and ends when a finish node is reached or it is stopped.
/// A run can also resume the active nodes another run of its graph saved, such as after a load.
/// </summary>
public sealed class FlowRun : IDisposable
{
    /// <summary>The most signals one trigger may pass on before the run is stopped as a loop.</summary>
    public const int MaxStepsPerTrigger = 10_000;

    private readonly Dictionary<FlowNode, List<FlowWire>[]> _wires = [];
    private readonly List<FlowNode> _active = [];
    private readonly Stack<FlowWire> _signals = [];
    private bool _dispatching;

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

    public ConditionContext Conditions { get; private set; }

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

    /// <summary>
    /// Starts the run where a run of the same graph was saved: each node of <paramref name="active"/> still in the graph is active
    /// again and resumes, in order, while nodes without inputs do not run again; ids the graph no longer has are skipped.
    /// </summary>
    public void Resume(IReadOnlyList<string> active)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("The flow run has already started.");
        }

        IsRunning = true;
        Conditions.Facts.FactChanged += HandleFactChanged;
        foreach (string id in active)
        {
            if (!IsRunning)
            {
                break;
            }

            FlowNode? node = Graph.Nodes.FirstOrDefault(node => node.Id == id);
            node?.Resume(this);
        }
    }

    /// <summary>Checks and applies the run's rules against <paramref name="conditions"/> from now on, such as for a player spawned again.</summary>
    public void SetConditions(ConditionContext conditions)
    {
        if (IsRunning)
        {
            Conditions.Facts.FactChanged -= HandleFactChanged;
            conditions.Facts.FactChanged += HandleFactChanged;
        }

        Conditions = conditions;
    }

    /// <summary>The ids of the active nodes, which a save keeps so that a later run of the graph can resume them.</summary>
    public IReadOnlyList<string> GetActiveIds()
    {
        List<string> ids = [];
        foreach (FlowNode node in _active)
        {
            ids.Add(node.Id);
        }

        return ids;
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

    /// <summary>
    /// Passes a signal from the node's output to every input wired to it, depth first in the links' order. Signals wait on a stack
    /// rather than nesting calls, so a long chain of nodes can't overflow the call stack.
    /// </summary>
    internal void Trigger(FlowNode node, int output)
    {
        if (!IsRunning)
        {
            return;
        }

        List<FlowWire> wires = _wires[node][output];
        for (int index = wires.Count - 1; index >= 0; index--)
        {
            _signals.Push(wires[index]);
        }

        if (_dispatching)
        {
            return;
        }

        _dispatching = true;
        int steps = 0;
        try
        {
            while (IsRunning && _signals.TryPop(out FlowWire wire))
            {
                steps++;
                if (steps > MaxStepsPerTrigger)
                {
                    Finish();
                    throw new InvalidOperationException($"The flow passed more than {MaxStepsPerTrigger} signals from {node.Id} without stopping; its links loop.");
                }

                wire.Target.Execute(this, wire.Input);
            }
        }
        finally
        {
            _signals.Clear();
            _dispatching = false;
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
