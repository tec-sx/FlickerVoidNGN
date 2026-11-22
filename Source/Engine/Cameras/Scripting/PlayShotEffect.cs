using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Plays a shot's timeline to a key, or to its end.</summary>
public sealed class PlayShotEffect : Effect
{
    /// <summary>The name of the shot entity; empty plays the live shot.</summary>
    public string Shot { get; set; } = string.Empty;

    /// <summary>The key to play to; -1 plays to the end.</summary>
    public int ToKey { get; set; } = -1;

    /// <summary>Whether the shot's layers move it while it plays.</summary>
    public bool Layers { get; set; } = true;

    public override void Apply(ConditionContext context)
    {
        CameraDirector? director = context.GetService<CameraDirector>();
        if (director is null)
        {
            return;
        }

        Entity shot = director.FindShotOrLive(Shot);
        director.Play(shot, ToKey, Layers);
    }

    public override string Describe() => $"Play {Shot}";
}
