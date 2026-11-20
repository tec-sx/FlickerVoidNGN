using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Splits a camera's view into a grid of clusters (screen tiles by depth slices) and lists the lights that reach each one,
/// so a pixel only shades the lights of its own cluster. Slices are even for an orthographic camera and deepen with
/// distance for a perspective one, where near clusters would otherwise be far deeper than they are wide.
/// </summary>
public sealed class LightClusters
{
    public const int TilesX = 16;
    public const int TilesY = 9;
    public const int Slices = 24;
    public const int Count = TilesX * TilesY * Slices;

    private readonly ClusterRange[] _ranges = new ClusterRange[Count];
    private readonly List<uint> _indices = [];
    private Vector4[] _viewLights = [];

    /// <summary>Whether slices split the logarithm of the depth (perspective) or the depth itself (orthographic).</summary>
    public bool LogarithmicSlices { get; private set; }

    /// <summary>A pixel's slice is floor(depth * SliceScale + SliceBias), with the depth's logarithm when slices are logarithmic.</summary>
    public float SliceScale { get; private set; }

    public float SliceBias { get; private set; }

    /// <summary>Each cluster's lights, indexed by (slice * TilesY + tileY) * TilesX + tileX; tile rows count from the top.</summary>
    public ReadOnlySpan<ClusterRange> Ranges => _ranges;

    /// <summary>The indices of the lights given to <see cref="Build"/>, cluster after cluster.</summary>
    public ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);

    /// <summary>Sorts the lights into the clusters of the camera's view, replacing the last build.</summary>
    public void Build(in Camera camera, in CameraView view, ReadOnlySpan<LightBounds> lights)
    {
        float near = camera.NearPlane;
        float far = camera.FarPlane;
        LogarithmicSlices = camera.Projection == CameraProjection.Perspective;
        if (LogarithmicSlices)
        {
            float logRange = MathF.Log(far / near);
            SliceScale = Slices / logRange;
            SliceBias = -Slices * MathF.Log(near) / logRange;
        }
        else
        {
            SliceScale = Slices / (far - near);
            SliceBias = -Slices * near / (far - near);
        }

        if (_viewLights.Length < lights.Length)
        {
            _viewLights = new Vector4[lights.Length];
        }

        Span<Vector4> viewLights = _viewLights.AsSpan(0, lights.Length);
        for (int index = 0; index < lights.Length; index++)
        {
            Vector3 center = Vector3.Transform(lights[index].Position, view.View);
            viewLights[index] = new Vector4(center.X, center.Y, -center.Z, lights[index].Range);
        }

        _indices.Clear();
        Vector2 extent = new(1 / view.Projection.M11, 1 / view.Projection.M22);
        for (int slice = 0; slice < Slices; slice++)
        {
            float sliceNear = GetSliceStart(slice, near, far);
            float sliceFar = GetSliceStart(slice + 1, near, far);
            for (int tileY = 0; tileY < TilesY; tileY++)
            {
                for (int tileX = 0; tileX < TilesX; tileX++)
                {
                    GetTileBounds(tileX, tileY, sliceNear, sliceFar, extent, out Vector3 min, out Vector3 max);
                    int first = _indices.Count;
                    for (int light = 0; light < viewLights.Length; light++)
                    {
                        Vector4 sphere = viewLights[light];
                        Vector3 center = new(sphere.X, sphere.Y, sphere.Z);
                        Vector3 closest = Vector3.Clamp(center, min, max);
                        if (Vector3.DistanceSquared(closest, center) <= sphere.W * sphere.W)
                        {
                            _indices.Add((uint)light);
                        }
                    }

                    int cluster = GetIndex(tileX, tileY, slice);
                    _ranges[cluster] = new ClusterRange((uint)first, (uint)(_indices.Count - first));
                }
            }
        }
    }

    /// <summary>The slice holding a view depth (distance in front of the camera), clamped to the grid.</summary>
    public int GetSlice(float depth)
    {
        float value = depth;
        if (LogarithmicSlices)
        {
            value = MathF.Log(MathF.Max(depth, float.Epsilon));
        }

        float slice = MathF.Floor((value * SliceScale) + SliceBias);
        return (int)Math.Clamp(slice, 0, Slices - 1);
    }

    public static int GetIndex(int tileX, int tileY, int slice) => (((slice * TilesY) + tileY) * TilesX) + tileX;

    public ReadOnlySpan<uint> GetLights(int tileX, int tileY, int slice)
    {
        int cluster = GetIndex(tileX, tileY, slice);
        ClusterRange range = _ranges[cluster];
        return Indices.Slice((int)range.Offset, (int)range.Count);
    }

    private float GetSliceStart(int slice, float near, float far)
    {
        float share = (float)slice / Slices;
        if (LogarithmicSlices)
        {
            return near * MathF.Pow(far / near, share);
        }

        return near + ((far - near) * share);
    }

    // The tile's box in view space, with z as depth in front of the camera. A perspective tile widens with depth, so its
    // box spans the tile's sides at the slice's far end.
    private void GetTileBounds(int tileX, int tileY, float sliceNear, float sliceFar, Vector2 extent, out Vector3 min, out Vector3 max)
    {
        float left = -1 + (2f * tileX / TilesX);
        float right = -1 + (2f * (tileX + 1) / TilesX);
        float top = 1 - (2f * tileY / TilesY);
        float bottom = 1 - (2f * (tileY + 1) / TilesY);
        Vector2 low = new Vector2(left, bottom) * extent;
        Vector2 high = new Vector2(right, top) * extent;
        if (LogarithmicSlices)
        {
            Vector2 lowNear = low * sliceNear;
            Vector2 lowFar = low * sliceFar;
            Vector2 highNear = high * sliceNear;
            Vector2 highFar = high * sliceFar;
            low = Vector2.Min(lowNear, lowFar);
            high = Vector2.Max(highNear, highFar);
        }

        min = new Vector3(low, sliceNear);
        max = new Vector3(high, sliceFar);
    }
}

