namespace FlickerVoid.Editor.Materials;

/// <summary>
/// How the preview shows a material: on which shape, turned by <paramref name="Yaw"/> radians about its vertical axis, in
/// which mood, with its sun or lamp turned by <paramref name="LightYaw"/> radians about the shape.
/// </summary>
public sealed record PreviewView(PreviewShape Shape, float Yaw, PreviewMood Mood = PreviewMood.Day, float LightYaw = 0);
