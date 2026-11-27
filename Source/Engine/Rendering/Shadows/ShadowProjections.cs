using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>The world-to-clip matrices shadows are drawn and looked up with, depth from 0 to 1 as for cameras.</summary>
public static class ShadowProjections
{
    public const int PointFaces = 6;

    private const float LocalNearPlane = 0.05f;

    // How much the world's box grows on every side before it cuts the view, and the smallest half size of the sun's map.
    private const float WorldMargin = 0.05f;
    private const float MinSunSize = 0.25f;

    private static readonly Vector3[] FaceDirections = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    private static readonly Vector3[] FaceUps = [Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY];

    /// <summary>
    /// An orthographic view along the sunlight round the part of the world the camera's view sees, cut at the shadow distance,
    /// reaching up to <paramref name="casterReach"/> further toward the sun for casters out of view; none where the view sees
    /// nothing of the world. It moves in steps of a sixteenth of its size, all whole texels of the sun's atlas tile, and of the reach
    /// in depth, and grows in steps of a fourth of an octave, so it stays the same while the camera moves within a step and a kept shadow stays good.
    /// </summary>
    public static Matrix4x4? FitSun(in Camera camera, in CameraView view, Vector3 sunDirection, float shadowDistance, float casterReach, in Bounds world)
    {
        if (world.IsEmpty)
        {
            return null;
        }

        Matrix4x4.Invert(view.ViewProjection, out Matrix4x4 toWorld);
        float depth = MathF.Min(shadowDistance, camera.FarPlane);
        Vector4 farClip = Vector4.Transform(new Vector4(0, 0, -depth, 1), view.Projection);
        float farZ = farClip.Z / farClip.W;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int index = 0; index < corners.Length; index++)
        {
            float x = (index & 1) == 0 ? -1 : 1;
            float y = (index & 2) == 0 ? -1 : 1;
            float z = (index & 4) == 0 ? 0 : farZ;
            Vector4 corner = Vector4.Transform(new Vector4(x, y, z, 1), toWorld);
            corners[index] = new Vector3(corner.X, corner.Y, corner.Z) / corner.W;
        }

        // A flat world, such as a single floor, still has a little thickness to cut the view with.
        Bounds grown = new(world.Min - new Vector3(WorldMargin), world.Max + new Vector3(WorldMargin));
        Span<Vector3> points = stackalloc Vector3[HullClip.MaxPoints];
        int count = HullClip.Clip(corners, grown, points);
        if (count == 0)
        {
            return null;
        }

        Vector3 direction = Vector3.Normalize(sunDirection);
        Vector3 up = PickUp(direction);
        Matrix4x4 lightView = Matrix4x4.CreateLookAt(Vector3.Zero, direction, up);
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (Vector3 point in points[..count])
        {
            Vector3 light = Vector3.Transform(point, lightView);
            min = Vector3.Min(min, light);
            max = Vector3.Max(max, light);
        }

        // Depth along the light is -z: the nearest to the sun is the largest z.
        float worldNearest = float.MinValue;
        for (int index = 0; index < 8; index++)
        {
            float x = (index & 1) == 0 ? grown.Min.X : grown.Max.X;
            float y = (index & 2) == 0 ? grown.Min.Y : grown.Max.Y;
            float z = (index & 4) == 0 ? grown.Min.Z : grown.Max.Z;
            Vector3 light = Vector3.Transform(new Vector3(x, y, z), lightView);
            worldNearest = MathF.Max(worldNearest, light.Z);
        }

        Vector2 extent = new(max.X - min.X, max.Y - min.Y);
        float half = MathF.Max(extent.X, extent.Y) / 2;
        float size = StepUp(half * 17 / 16);
        float step = size / 16;
        float centerX = MathF.Round((min.X + max.X) / 2 / step) * step;
        float centerY = MathF.Round((min.Y + max.Y) / 2 / step) * step;
        // Depth moves in steps as long as the reach toward the sun, so it rarely changes as the camera moves.
        float depthStep = MathF.Max(casterReach, size / 4);
        float nearest = MathF.Max(-worldNearest, -max.Z - casterReach);
        float near = MathF.Floor(nearest / depthStep) * depthStep;
        float far = MathF.Max(MathF.Ceiling(-min.Z / depthStep), (near / depthStep) + 1) * depthStep;
        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(centerX - size, centerX + size, centerY - size, centerY + size, near, far);
        return lightView * projection;
    }

    /// <summary>A spot light's view down its entity's -Z, with +Y up, as wide as its outer cone.</summary>
    public static Matrix4x4 Spot(Vector3 position, Quaternion rotation, float outerConeAngle, float range)
    {
        Vector3 direction = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 up = Vector3.Transform(Vector3.UnitY, rotation);
        Matrix4x4 view = Matrix4x4.CreateLookAt(position, position + direction, up);
        float fieldOfView = MathF.Min(float.DegreesToRadians(2 * outerConeAngle), float.DegreesToRadians(170));
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, 1, LocalNearPlane, range);
        return view * projection;
    }

    /// <summary>One of a point light's six 90 degree views, looking down +X, -X, +Y, -Y, +Z and -Z in turn.</summary>
    public static Matrix4x4 PointFace(Vector3 position, int face, float range)
    {
        Vector3 direction = FaceDirections[face];
        Vector3 up = FaceUps[face];
        Matrix4x4 view = Matrix4x4.CreateLookAt(position, position + direction, up);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, LocalNearPlane, range);
        return view * projection;
    }

    /// <summary>The face of a point light whose view holds the offset from the light, by its largest axis.</summary>
    public static int GetPointFace(Vector3 offset)
    {
        Vector3 size = Vector3.Abs(offset);
        if (size.X >= size.Y && size.X >= size.Z)
        {
            return offset.X >= 0 ? 0 : 1;
        }

        if (size.Y >= size.Z)
        {
            return offset.Y >= 0 ? 2 : 3;
        }

        return offset.Z >= 0 ? 4 : 5;
    }

    /// <summary>
    /// Maps clip space onto the tile's texture coordinates (0 to 1 across the whole atlas, v down), keeping depth; applied
    /// after a shadow's projection, so the perspective divide yields atlas coordinates.
    /// </summary>
    public static Matrix4x4 ToAtlasTile(ShadowTile tile)
    {
        float scale = (float)tile.Size / ShadowAtlas.Size;
        float offsetX = (float)tile.X / ShadowAtlas.Size;
        float offsetY = (float)tile.Y / ShadowAtlas.Size;
        Matrix4x4 toTile = Matrix4x4.Identity;
        toTile.M11 = 0.5f * scale;
        toTile.M22 = -0.5f * scale;
        toTile.M41 = (0.5f * scale) + offsetX;
        toTile.M42 = (0.5f * scale) + offsetY;
        return toTile;
    }

    /// <summary>The size at or above it in steps of a fourth of an octave.</summary>
    private static float StepUp(float size)
    {
        float steps = MathF.Ceiling(MathF.Log2(MathF.Max(size, MinSunSize)) * 4);
        return MathF.Pow(2, steps / 4);
    }

    private static Vector3 PickUp(Vector3 direction)
    {
        if (MathF.Abs(direction.Y) > 0.99f)
        {
            return Vector3.UnitZ;
        }

        return Vector3.UnitY;
    }
}

