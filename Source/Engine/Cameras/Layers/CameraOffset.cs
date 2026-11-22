using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>
/// A small change to a posed camera, in the camera's own space: a move (+X right, +Y up, +Z back), a turn in degrees (pitch, yaw,
/// roll) and a zoom as a fraction of the lens's (-0.1 is 10% closer).
/// </summary>
public readonly record struct CameraOffset(Vector3 Position, Vector3 Rotation, float Zoom)
{
    public static CameraOffset None => default;

    public static CameraOffset operator +(CameraOffset left, CameraOffset right)
    {
        return new CameraOffset(left.Position + right.Position, left.Rotation + right.Rotation, left.Zoom + right.Zoom);
    }
}
