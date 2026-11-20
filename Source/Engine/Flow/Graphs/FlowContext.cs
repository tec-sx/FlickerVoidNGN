using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Flow.Graphs;

/// <summary>What a node works with while it runs: its run, itself and the condition context its rules are checked against.</summary>
public readonly struct FlowContext(FlowRun run, FlowNode node)
{
    public FlowRun Run { get; } = run;

    public FlowNode Node { get; } = node;

    public ConditionContext Conditions => Run.Conditions;

    /// <summary>Passes the signal on through the node's output; with <paramref name="finish"/>, the node stops being active first.</summary>
    public void Trigger(string output, bool finish = true)
    {
        if (finish)
        {
            Run.Deactivate(Node);
        }

        Run.Trigger(Node, output);
    }

    /// <summary>Keeps the node active, such as one waiting for conditions, until it triggers with finish or the run ends.</summary>
    public void StayActive() => Run.Activate(Node);

    /// <summary>Ends the whole run.</summary>
    public void FinishRun() => Run.Finish();
}
