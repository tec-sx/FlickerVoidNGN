using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Switches to, pushes or returns from a shot with a transition, then passes the signal on, once the transition ends unless told not to wait.</summary>
public sealed class SwitchShotNode : FlowNode<FlowOutput>
{
    /// <summary>The name of the shot entity; a return needs none.</summary>
    public string Shot { get; set; } = string.Empty;

    public ShotSwitchMode Mode { get; set; }

    public ShotTransition Transition { get; set; } = ShotTransition.Cut;

    public bool Wait { get; set; } = true;

    public override string Describe() => $"{Mode} {Shot} ({Transition.Kind})";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        CameraDirector? director = context.Conditions.GetService<CameraDirector>();
        if (director is null)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        Entity shot = director.FindShot(Shot);
        director.Request(Mode, shot, Transition);
        if (!Wait)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        context.StayActive();
        FlowWaiter owner = new(context.Run, this);
        director.WhenTransitionEnds(owner, () => context.Trigger(FlowOutput.Out));
    }

    /// <summary>Passes the signal on, as the save keeps the shot it switched to.</summary>
    protected override void Resume(FlowContext<FlowOutput> context) => context.Trigger(FlowOutput.Out);

    protected override void Cleanup(FlowContext<FlowOutput> context) => FlowWaiter.Cancel(context.Run, this, context.Conditions);
}
