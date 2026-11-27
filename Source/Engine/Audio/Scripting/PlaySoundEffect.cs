using FlickerVoid.Engine.Audio.Sounds;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Audio.Scripting;

/// <summary>Plays a sound where the effect's target stands, or as it is without one, such as a pickup's chime.</summary>
public sealed class PlaySoundEffect : Effect
{
    /// <summary>The <c>sound</c> data asset.</summary>
    [ContentFile(".fvsound", ".fvdata")]
    public string Sound { get; set; } = string.Empty;

    public override void Apply(ConditionContext context)
    {
        IAudioEngine? audio = context.GetService<IAudioEngine>();
        if (audio is null)
        {
            return;
        }

        if (context.Target is { } target && target.TryGet(out Transform transform))
        {
            audio.Play(Sound, transform.Position);
        }
        else
        {
            audio.Play(Sound);
        }
    }

    public override string Describe() => $"Play {Sound}";
}
