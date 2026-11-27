using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a sound file once over the music, from the next line of its grid, and passes the signal straight on.</summary>
public sealed class PlayStingerNode : FlowNode<FlowOutput>
{
    [ContentFile(".wav", ".ogg")]
    public string File { get; set; } = string.Empty;

    public MusicQuantize Quantize { get; set; } = MusicQuantize.Beat;

    public float Gain { get; set; } = 1;

    public override string Describe() => $"Play the stinger {File}";

    protected override void Execute(FlowContext<FlowOutput> context)
    {
        IMusicPlayer? music = context.Conditions.GetService<IMusicPlayer>();
        music?.PlayStinger(File, Quantize, Gain);
        context.Trigger(FlowOutput.Out);
    }
}
