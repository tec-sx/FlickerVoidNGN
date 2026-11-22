using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Definitions;

/// <summary>
/// One image of a map and the ground it covers, such as a floor of a building or the streets of a town: <see cref="Min"/> and
/// <see cref="Max"/> are world X and Z, and the image's top is north (-Z) and its right edge east (+X).
/// </summary>
public sealed class MapLayer
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [ContentFile(".png")]
    public string Image { get; set; } = string.Empty;

    /// <summary>The west and north edges, as world X and Z.</summary>
    public Vector2 Min { get; set; } = new(-500, -500);

    /// <summary>The east and south edges, as world X and Z.</summary>
    public Vector2 Max { get; set; } = new(500, 500);

    /// <summary>Covers only heights between <see cref="MinHeight"/> and <see cref="MaxHeight"/>, so floors of one building can share ground.</summary>
    public bool LimitHeight { get; set; }

    public float MinHeight { get; set; } = -10;

    public float MaxHeight { get; set; } = 10;

    public Vector2 Size => Max - Min;

    public bool ContainsGround(Vector3 point) => point.X >= Min.X && point.X <= Max.X && point.Z >= Min.Y && point.Z <= Max.Y;

    public bool Contains(Vector3 point)
    {
        if (!ContainsGround(point))
        {
            return false;
        }

        return !LimitHeight || (point.Y >= MinHeight && point.Y <= MaxHeight);
    }

    /// <summary>Where a point lies on the image, from 0 to 1 across and down.</summary>
    public Vector2 WorldToUv(Vector3 point)
    {
        Vector2 size = Size;
        if (size.X <= float.Epsilon || size.Y <= float.Epsilon)
        {
            return new Vector2(0.5f);
        }

        return new Vector2((point.X - Min.X) / size.X, (point.Z - Min.Y) / size.Y);
    }

    /// <summary>Half of the image, across and down, that shows the given metres around a point.</summary>
    public Vector2 RadiusToExtentUv(float radius)
    {
        Vector2 size = Size;
        return new Vector2(radius / MathF.Max(size.X, 1), radius / MathF.Max(size.Y, 1));
    }

    public Vector3 UvToWorld(Vector2 uv, float height)
    {
        Vector2 size = Size;
        return new Vector3(Min.X + (uv.X * size.X), height, Min.Y + (uv.Y * size.Y));
    }
}
