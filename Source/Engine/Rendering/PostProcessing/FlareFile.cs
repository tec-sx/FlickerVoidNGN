using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

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
}
