using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Switches to, pushes or returns from a shot with a transition, such as a door's Use fading to the room beyond.</summary>
public sealed class CutToShotEffect : Effect
{
    /// <summary>The name of the shot entity; a return needs none.</summary>
    public string Shot { get; set; } = string.Empty;

    public ShotSwitchMode Mode { get; set; }

    public ShotTransition Transition { get; set; } = ShotTransition.Cut;

    public override void Apply(ConditionContext context)
    {
        CameraDirector? director = context.GetService<CameraDirector>();
        if (director is null)
        {
            return;
        }

        Entity shot = director.FindShot(Shot);
        director.Request(Mode, shot, Transition);
    }

    public override string Describe() => $"{Mode} {Shot}";
}
