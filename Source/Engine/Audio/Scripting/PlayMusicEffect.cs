using FlickerVoid.Engine.Audio.Music;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a cue as its bus's music or ambience, taking the place of the one there on its next bar.</summary>
public sealed class PlayMusicEffect : Effect
{
    /// <summary>The <c>music</c> data asset.</summary>
    [ContentFile(".fvdata")]
    public string Cue { get; set; } = string.Empty;

    public override void Apply(ConditionContext context)
    {
        IMusicPlayer? music = context.GetService<IMusicPlayer>();
        music?.Play(Cue);
    }

    public override string Describe() => $"Play the music {Cue}";
}
