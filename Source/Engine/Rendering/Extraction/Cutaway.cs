namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// Marks the indoor volume the camera looks into: whatever stands between the camera and its box is cut away from the view
/// (never from shadows), and the world outside the box is muted by its <see cref="Lighting.IndoorVolume.OutsideMuted"/>.
/// </summary>
public readonly record struct Cutaway;
