using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Plays a shot's timeline to a key, or to its end, then passes the signal on, once it gets there unless told not to wait.</summary>
public sealed class PlayShotNode : FlowNode<FlowOutput>
{
    /// <summary>The name of the shot entity; empty plays the live shot.</summary>
    public string Shot { get; set; } = string.Empty;

    /// <summary>The key to play to; -1 plays to the end.</summary>
    public int ToKey { get; set; } = -1;

    /// <summary>Whether the shot's layers move it while it plays; cutscenes usually turn them off.</summary>
    public bool Layers { get; set; } = true;

    public bool Wait { get; set; } = true;

    public override string Describe() => $"Play {Shot}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        CameraDirector? director = context.Conditions.GetService<CameraDirector>();
        if (director is null)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        Entity shot = director.FindShotOrLive(Shot);
        director.Play(shot, ToKey, Layers);
        if (!Wait)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        context.StayActive();
        FlowWaiter owner = new(context.Run, this);
        director.WhenShotFinishes(owner, shot, () => context.Trigger(FlowOutput.Out));
    }

    protected override void Cleanup(FlowContext<FlowOutput> context) => FlowWaiter.Cancel(context.Run, this, context.Conditions);
}
