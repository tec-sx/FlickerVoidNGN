namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Shakes the live camera at a strength from 0 to 1, fading over the seconds given; its rig's shake layer, or a default one, decides how.</summary>
public readonly record struct CameraShakeCommand(float Strength, float Duration);
