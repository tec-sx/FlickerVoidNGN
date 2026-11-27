using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a sound file once over the music, from the next line of its grid, such as a chord when a key is found.</summary>
public sealed class PlayStingerEffect : Effect
{
    [ContentFile(".wav", ".ogg")]
    public string File { get; set; } = string.Empty;

    public MusicQuantize Quantize { get; set; } = MusicQuantize.Beat;

    public float Gain { get; set; } = 1;

    public override void Apply(ConditionContext context)
    {
        IMusicPlayer? music = context.GetService<IMusicPlayer>();
        music?.PlayStinger(File, Quantize, Gain);
    }

    public override string Describe() => $"Play the stinger {File}";
}
