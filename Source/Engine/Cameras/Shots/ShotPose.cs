using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>Where a camera is, where it looks and how far it zooms (orthographic height or field of view).</summary>
public readonly record struct ShotPose(Vector3 Position, Quaternion Rotation, float Zoom)
{
    /// <summary>The pose, given in a shot's space, as the lens and world pose the shot's camera is drawn with.</summary>
    public RenderCamera ToWorld(in Transform shot, Camera lens)
    {
        Matrix4x4 shotWorld = shot.ToMatrix();
        Vector3 position = Vector3.Transform(Position, shotWorld);
        Quaternion rotation = Quaternion.Concatenate(Rotation, shot.Rotation);
        if (lens.Projection == CameraProjection.Perspective)
        {
            lens.FieldOfView = Zoom;
        }
        else
        {
            lens.OrthographicHeight = Zoom;
        }

        Transform pose = new() { Position = position, Rotation = rotation };
        return new RenderCamera(lens, pose);
    }

    /// <summary>The rotation looking from a point at another with +Y up, or the fallback when they meet or the look is straight up or down.</summary>
    public static Quaternion LookAt(Vector3 from, Vector3 to, Quaternion fallback)
    {
        Vector3 toTarget = to - from;
        if (toTarget.LengthSquared() < 1e-8f)
        {
            return fallback;
        }

        Vector3 forward = Vector3.Normalize(toTarget);
        float upright = MathF.Abs(Vector3.Dot(forward, Vector3.UnitY));
        if (upright > 0.9999f)
        {
            return fallback;
        }

        Matrix4x4 world = Matrix4x4.CreateWorld(Vector3.Zero, forward, Vector3.UnitY);
        return Quaternion.CreateFromRotationMatrix(world);
    }

    /// <summary>A world pose as a pose in a shot's space, keeping the zoom given.</summary>
    public static ShotPose FromWorld(in Transform world, in Transform shot, float zoom)
    {
        Matrix4x4 shotWorld = shot.ToMatrix();
        Matrix4x4.Invert(shotWorld, out Matrix4x4 toShot);
        Vector3 position = Vector3.Transform(world.Position, toShot);
        Quaternion inverse = Quaternion.Inverse(shot.Rotation);
        Quaternion rotation = Quaternion.Concatenate(world.Rotation, inverse);
        Quaternion normalized = Quaternion.Normalize(rotation);
        return new ShotPose(position, normalized, zoom);
    }
}
