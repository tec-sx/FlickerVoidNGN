using System.ComponentModel.DataAnnotations;
using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// A pocket of fog standing on its entity, as an indoor volume does: mist over a pond, smoke in a room, damp in a cellar. Its
/// density adds to the mood's in the lit fog, so the sun, lamps and probes light it as they light the rest of the air.
/// </summary>
public struct FogVolume
{
    public FogVolume()
    {
    }

    public FogShape Shape { get; set; }

    /// <summary>Width, height and length of the box the fog fills.</summary>
    public Vector3 Size { get; set; } = new(4, 3, 4);

    /// <summary>How much of the light crossing a world unit of it the fog absorbs or scatters, at its densest.</summary>
    [Range(0, 10)]
    public float Density { get; set; } = 0.2f;

    /// <summary>The colour the fog scatters light with, as the mood's fog colour does.</summary>
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>The share of each half size, from the edge inward, over which the fog thins to nothing at the edge.</summary>
    [Range(0, 1)]
    public float EdgeSoftness { get; set; } = 0.3f;

    /// <summary>How fast the fog thins upward from the bottom of its box: by e every 1 / falloff units; 0 keeps it even.</summary>
    [Range(0, 10)]
    public float HeightFalloff { get; set; }

    /// <summary>How far the mood's drifting noise breaks the fog up: 0 keeps it smooth, 1 tears it into wisps.</summary>
    [Range(0, 1)]
    public float Noise { get; set; }
}
