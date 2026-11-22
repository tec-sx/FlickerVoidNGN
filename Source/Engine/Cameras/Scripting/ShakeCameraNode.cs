using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Shakes the live camera, fading over the duration, and passes the signal straight on.</summary>
public sealed class ShakeCameraNode : FlowNode<FlowOutput>
{
    /// <summary>From 0 to 1.</summary>
    public float Strength { get; set; } = 0.5f;

    public float Duration { get; set; } = 0.5f;

    public override string Describe() => $"Shake {Strength}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        CameraDirector? director = context.Conditions.GetService<CameraDirector>();
        director?.Shake(Strength, Duration);
        context.Trigger(FlowOutput.Out);
    }
}
