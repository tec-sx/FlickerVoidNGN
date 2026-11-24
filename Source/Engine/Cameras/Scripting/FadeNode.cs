using System.Numerics;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Fades the world out to a colour, or back in, then passes the signal on, once the fade ends unless told not to wait.</summary>
public sealed class FadeNode : FlowNode<FlowOutput>
{
    public FadeDirection Direction { get; set; }

    public float Duration { get; set; } = 0.5f;

    /// <summary>The colour fading out goes to, in display values; black by default.</summary>
    public Vector3 Color { get; set; }

    public bool Wait { get; set; } = true;

    public override string Describe() => $"Fade {Direction}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        CameraDirector? director = context.Conditions.GetService<CameraDirector>();
        if (director is null)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        if (Direction == FadeDirection.Out)
        {
            director.FadeOut(Duration, Color);
        }
        else
        {
            director.FadeIn(Duration);
        }

        if (!Wait)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        context.StayActive();
        FlowWaiter owner = new(context.Run, this);
        director.WhenTransitionEnds(owner, () => context.Trigger(FlowOutput.Out));
    }

    /// <summary>Passes the signal on, as the save keeps the fade.</summary>
    protected override void Resume(FlowContext<FlowOutput> context) => context.Trigger(FlowOutput.Out);

    protected override void Cleanup(FlowContext<FlowOutput> context) => FlowWaiter.Cancel(context.Run, this, context.Conditions);
}
