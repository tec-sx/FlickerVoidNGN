using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>
/// A <c>Name.fvcanvas</c> file: the canvas everything is painted on, an image laid flat under the scene, moving with the world as
/// the camera pans and zooms, shown unlit where nothing is painted and through paint that isn't thick, with how it repeats
/// and its tint.
/// </summary>
public sealed record CanvasFile
{
    public const string Extension = ".fvcanvas";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    /// <summary>The canvas's image: a picture or a painting in the content folder.</summary>
    [ContentFile(".png", ".jpg", ".jpeg", ".tga", ".bmp", ".reb")]
    public string? Image { get; set; }

    /// <summary>How many times the image repeats up the screen's height, at an orthographic height of 10 units; more makes the weave finer.</summary>
    [Range(0.1, 16)]
    public float Tiling { get; set; } = 1;

    /// <summary>How strongly the image's texture shows: 1 as painted, 0 its average colour alone, above 1 stronger.</summary>
    [Range(0, 2)]
    public float Intensity { get; set; } = 1;

    /// <summary>Multiplies the image's colour.</summary>
    [Color]
    public Vector3 Tint { get; set; } = Vector3.One;
}
