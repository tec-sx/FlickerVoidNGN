using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>
/// One run of a flow graph, like a Flow plugin asset instance: it starts at the graph's start nodes, passes signals along the
/// links, keeps latent nodes active and tells them when facts change, and ends when a finish node is reached or it is stopped.
/// </summary>
public sealed class FlowRun : IDisposable
{
    /// <summary>The most signals one trigger may pass on before the run is stopped as a loop.</summary>
    public const int MaxStepsPerTrigger = 10_000;

    private readonly List<FlowNode> _active = [];
    private int _depth;
    private int _steps;

    public FlowRun(FlowGraph graph, ConditionContext conditions)
    {
        Graph = graph;
        Conditions = conditions;
    }

    /// <summary>Raised once when the run ends, by a finish node or <see cref="Finish"/>.</summary>
    public event Action<FlowRun>? Finished;

    public FlowGraph Graph { get; }

    public ConditionContext Conditions { get; }

    public bool IsRunning { get; private set; }

    public IReadOnlyList<FlowNode> ActiveNodes => _active;

    /// <summary>Starts the run: every start node's output fires, in the graph's order.</summary>
    public void Start()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("The flow run has already started.");
        }

        IsRunning = true;
        Conditions.Facts.FactChanged += HandleFactChanged;
        foreach (FlowNode node in Graph.Nodes.OfType<Nodes.StartNode>().ToList())
        {
            if (!IsRunning)
            {
                break;
            }

            node.Execute(new FlowContext(this, node), FlowNode.DefaultInput);
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
            node.Cleanup(new FlowContext(this, node));
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
            node.Cleanup(new FlowContext(this, node));
        }
    }

    /// <summary>Passes a signal from the node's output to every input linked to it, in the links' order.</summary>
    internal void Trigger(FlowNode node, string output)
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
            foreach (FlowLink link in Graph.Links.Where(link => link.From == node.Id && link.Output == output).ToList())
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

                FlowNode? target = Graph.FindNode(link.To);
                if (target is null)
                {
                    throw new InvalidDataException($"A link from {node.Id} leads to the missing node {link.To}.");
                }

                target.Execute(new FlowContext(this, target), link.Input);
            }
        }
        finally
        {
            _depth--;
        }
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
                node.OnFactChanged(new FlowContext(this, node));
            }
        }
    }
}
