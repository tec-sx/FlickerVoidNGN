using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Cameras.Scripting;

/// <summary>Shakes the live camera, fading over the duration.</summary>
public sealed class ShakeCameraEffect : Effect
{
    /// <summary>From 0 to 1.</summary>
    public float Strength { get; set; } = 0.5f;

    public float Duration { get; set; } = 0.5f;

    public override void Apply(ConditionContext context)
    {
        CameraDirector? director = context.GetService<CameraDirector>();
        director?.Shake(Strength, Duration);
    }

    public override string Describe() => $"Shake {Strength}";
}
