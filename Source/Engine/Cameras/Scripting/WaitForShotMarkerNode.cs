using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Holds the signal until a shot reaches a key with the marker.</summary>
public sealed class WaitForShotMarkerNode : FlowNode<FlowOutput>
{
    /// <summary>The name of the shot entity; empty waits for the marker on any shot.</summary>
    public string Shot { get; set; } = string.Empty;

    public Tag Marker { get; set; }

    public override string Describe() => Marker.Name;

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        CameraDirector? director = context.Conditions.GetService<CameraDirector>();
        if (director is null)
        {
            context.Trigger(FlowOutput.Out);
            return;
        }

        Entity shot = director.FindShot(Shot);
        context.StayActive();
        FlowWaiter owner = new(context.Run, this);
        director.WhenMarker(owner, shot, Marker, () => context.Trigger(FlowOutput.Out));
    }

    protected override void Cleanup(FlowContext<FlowOutput> context) => FlowWaiter.Cancel(context.Run, this, context.Conditions);
}
