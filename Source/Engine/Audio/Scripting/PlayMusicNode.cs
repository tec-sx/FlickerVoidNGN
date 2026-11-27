using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a cue as its bus's music or ambience, taking the place of the one there on its next bar, and passes the signal straight on.</summary>
public sealed class PlayMusicNode : FlowNode<FlowOutput>
{
    /// <summary>The <c>music</c> data asset.</summary>
    [ContentFile(".fvmusic", ".fvdata")]
    public string Cue { get; set; } = string.Empty;

    public override string Describe() => $"Play the music {Cue}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        IMusicPlayer? music = context.Conditions.GetService<IMusicPlayer>();
        music?.Play(Cue);
        context.Trigger(FlowOutput.Out);
    }
}
