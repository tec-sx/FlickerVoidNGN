using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// A room's or a storey's box standing on its entity, as a greybox does. Inside it the mood's even ambient light is scaled by
/// <see cref="Ambient"/>, so a closed room is dark but for the light its windows, doors, holes and lamps let in. Its walls
/// and roof keep blocking the sun and lights through the shadows, even while they are cut away from the view.
/// </summary>
public struct IndoorVolume
{
    public IndoorVolume()
    {
    }

    /// <summary>Width, height and length, up to the inner faces of the walls, floor and ceiling.</summary>
    public Vector3 Size { get; set; } = new(4, 3, 4);

    /// <summary>The share of the mood's ambient light that reaches inside.</summary>
    [Range(0, 1)]
    public float Ambient { get; set; } = 0.1f;

    /// <summary>While the camera looks into this volume, how far the world outside it is greyed and darkened.</summary>
    [Range(0, 1)]
    public float OutsideMuted { get; set; }

    /// <summary>The box round the volume in world space, standing on <paramref name="position"/> and turned by <paramref name="rotation"/>.</summary>
    public readonly (Vector3 Min, Vector3 Max) GetBounds(Vector3 position, Quaternion rotation)
    {
        Vector3 half = Size / 2;
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            float x = (corner & 1) == 0 ? -half.X : half.X;
            float y = (corner & 2) == 0 ? 0 : Size.Y;
            float z = (corner & 4) == 0 ? -half.Z : half.Z;
            Vector3 local = new(x, y, z);
            Vector3 point = position + Vector3.Transform(local, rotation);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min, max);
    }
}
