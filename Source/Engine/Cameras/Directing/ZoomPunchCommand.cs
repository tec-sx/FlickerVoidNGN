namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>Punches the live camera's zoom at a strength from 0 to 1; its rig's zoom punch layer, or a default one, decides how.</summary>
public readonly record struct ZoomPunchCommand(float Strength);
