using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a sound where the run's target stands, or as it is without one, and passes the signal straight on.</summary>
public sealed class PlaySoundNode : FlowNode<FlowOutput>
{
    /// <summary>The <c>sound</c> data asset.</summary>
    [ContentFile(".fvsound", ".fvdata")]
    public string Sound { get; set; } = string.Empty;

    public override string Describe() => $"Play {Sound}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        IAudioEngine? audio = context.Conditions.GetService<IAudioEngine>();
        if (audio is not null)
        {
            if (context.Conditions.Target is { } target && target.TryGet(out Transform transform))
            {
                audio.Play(Sound, transform.Position);
            }
            else
            {
                audio.Play(Sound);
            }
        }

        context.Trigger(FlowOutput.Out);
    }
}
