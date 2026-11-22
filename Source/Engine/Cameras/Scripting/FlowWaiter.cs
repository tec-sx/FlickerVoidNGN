using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Who waits on the director: a node of a run, so its wait ends when the node stops being active.</summary>
internal readonly record struct FlowWaiter(FlowRun Run, FlowNode Node)
{
    public static void Cancel(FlowRun run, FlowNode node, ConditionContext conditions)
    {
        CameraDirector? director = conditions.GetService<CameraDirector>();
        FlowWaiter owner = new(run, node);
        director?.CancelWaits(owner);
    }
}
