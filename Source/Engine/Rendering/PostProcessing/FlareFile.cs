using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// A <c>Name.fvflare</c> file: the glass of a lens flare, after Froyok's: the ghosts it copies bright lights into, how far their
/// colours split and how they fade at the screen's edge. How strong the flare is and what is bright enough sit in the lens.
/// </summary>
public sealed record FlareFile
{
    public const string Extension = ".fvflare";

    /// <summary>The most ghosts a flare draws; more are left out.</summary>
    public const int MaxGhosts = 8;

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The ghosts, at most eight.</summary>
    public List<FlareGhost> Ghosts { get; set; } =
    [
        new() { Scale = -1.6f, Color = new Vector3(0.12f, 0.08f, 0.05f) },
        new() { Scale = -1.0f, Color = new Vector3(0.08f, 0.1f, 0.14f) },
        new() { Scale = -0.7f, Color = new Vector3(0.1f, 0.12f, 0.08f) },
        new() { Scale = -0.45f, Color = new Vector3(0.14f, 0.1f, 0.06f) },
        new() { Scale = -0.25f, Color = new Vector3(0.06f, 0.08f, 0.12f) },
        new() { Scale = 0.3f, Color = new Vector3(0.05f, 0.06f, 0.08f) },
        new() { Scale = 0.6f, Color = new Vector3(0.06f, 0.05f, 0.04f) },
        new() { Scale = 1.4f, Color = new Vector3(0.04f, 0.05f, 0.06f) },
    ];

    /// <summary>How far, in screen widths, red and blue part from green away from the middle.</summary>
    [Range(0, 0.05)]
    public float ChromaShift { get; set; } = 0.004f;

    /// <summary>How much of the screen, from each edge in, a ghost fades over as it leaves the image.</summary>
    [Range(0.01, 0.5)]
    public float EdgeFade { get; set; } = 0.1f;

    /// <summary>How far from the middle, in screen heights, the halo's ring lies.</summary>
    [Range(0, 1)]
    public float HaloRadius { get; set; } = 0.45f;

    /// <summary>How wide the halo's ring is, in screen heights.</summary>
    [Range(0.001, 0.5)]
    public float HaloWidth { get; set; } = 0.05f;

    /// <summary>How strongly the halo shows; 0 draws none.</summary>
    [Range(0, 4)]
    public float HaloIntensity { get; set; } = 0.1f;

    /// <summary>How far, in screen heights, the halo's red and blue rings part from its green one.</summary>
    [Range(0, 0.1)]
    public float HaloChroma { get; set; } = 0.01f;

    /// <summary>The flare's colour in the middle of the screen.</summary>
    [Color]
    public Vector3 CenterColor { get; set; } = Vector3.One;

    /// <summary>The flare's colour at the screen's edge, blended from the middle's.</summary>
    [Color]
    public Vector3 EdgeColor { get; set; } = Vector3.One;
}
