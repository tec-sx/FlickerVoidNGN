using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// A <c>Name.fvcanvas</c> file: the canvas everything is painted on, an image wrapped around the camera like a sky and shown
/// unlit where nothing is painted and through paint that isn't thick, with how it repeats, its tint, its grain and how
/// curved it looks.
/// </summary>
public sealed record CanvasFile
{
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The canvas's image: a picture or a painting in the content folder.</summary>
    [ContentFile(".png", ".jpg", ".jpeg", ".tga", ".bmp", ".reb")]
    public string? Image { get; set; }

    /// <summary>How many times the image repeats up the screen's height; more makes the weave finer.</summary>
    [Range(0.1, 16)]
    public float Tiling { get; set; } = 1;

    /// <summary>Multiplies the image's colour.</summary>
    [Color]
    public Vector3 Tint { get; set; } = Vector3.One;

    /// <summary>How much the canvas's grain decides where thin paint lies: it pools in the dark valleys and leaves the light peaks bare.</summary>
    [Range(0, 1)]
    public float Grain { get; set; } = 0.3f;

    /// <summary>How curved the sphere the canvas is wrapped on looks: 0 is all but flat, 1 a wide lens.</summary>
    [Range(0, 1)]
    public float Curvature { get; set; } = 0.15f;
}
