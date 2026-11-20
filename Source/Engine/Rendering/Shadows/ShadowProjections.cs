using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.Shadows;

/// <summary>The world-to-clip matrices shadows are drawn and looked up with, depth from 0 to 1 as for cameras.</summary>
public static class ShadowProjections
{
    public const int PointFaces = 6;

    private const float LocalNearPlane = 0.05f;

    private static readonly Vector3[] FaceDirections = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    private static readonly Vector3[] FaceUps = [Vector3.UnitY, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitY, Vector3.UnitY];

    /// <summary>
    /// An orthographic view along the sunlight around the camera's view, cut at the shadow distance, reaching further toward
    /// the sun for casters out of view. It fits a sphere round the view, so its size stays the same as the camera turns,
    /// and moves in whole texels of the sun's atlas tile, so shadow edges don't crawl as the camera moves.
    /// </summary>
    public static Matrix4x4 FitSun(in Camera camera, in CameraView view, Vector3 sunDirection, float shadowDistance, float casterReach)
    {
        Matrix4x4.Invert(view.ViewProjection, out Matrix4x4 toWorld);
        float depth = MathF.Min(shadowDistance, camera.FarPlane);
        Vector4 farClip = Vector4.Transform(new Vector4(0, 0, -depth, 1), view.Projection);
        float farZ = farClip.Z / farClip.W;

        Span<Vector3> corners = stackalloc Vector3[8];
        Vector3 center = Vector3.Zero;
        for (int index = 0; index < corners.Length; index++)
        {
            float x = (index & 1) == 0 ? -1 : 1;
            float y = (index & 2) == 0 ? -1 : 1;
            float z = (index & 4) == 0 ? 0 : farZ;
            Vector4 corner = Vector4.Transform(new Vector4(x, y, z, 1), toWorld);
            corners[index] = new Vector3(corner.X, corner.Y, corner.Z) / corner.W;
            center += corners[index];
        }

        center /= corners.Length;
        float radius = 0;
        foreach (Vector3 corner in corners)
        {
            float distance = Vector3.Distance(corner, center);
            radius = MathF.Max(radius, distance);
        }

        Vector3 direction = Vector3.Normalize(sunDirection);
        Vector3 up = PickUp(direction);
        Matrix4x4 lightView = Matrix4x4.CreateLookAt(Vector3.Zero, direction, up);
        Vector3 lightCenter = Vector3.Transform(center, lightView);
        float texel = 2 * radius / ShadowAtlas.SunTileSize;
        float snappedX = MathF.Round(lightCenter.X / texel) * texel;
        float snappedY = MathF.Round(lightCenter.Y / texel) * texel;
        float centerDepth = -lightCenter.Z;
        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(
            snappedX - radius,
            snappedX + radius,
            snappedY - radius,
            snappedY + radius,
            centerDepth - radius - casterReach,
            centerDepth + radius);
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

    private static Vector3 PickUp(Vector3 direction)
    {
        if (MathF.Abs(direction.Y) > 0.99f)
        {
            return Vector3.UnitZ;
        }

        return Vector3.UnitY;
    }
}

