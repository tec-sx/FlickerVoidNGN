using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A <c>Name.png.fvtexture</c> file beside an image: how the content build makes a texture of it, cropped, shrunk to a size
/// limit, tinted and compressed. An image without one is built whole, untinted, as BC7.
/// </summary>
public sealed record TextureSettings
{
    /// <summary>The extension a settings file adds to its image's name.</summary>
    public const string Extension = ".fvtexture";

    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    public string? Schema { get; set; }

    public TextureCompression Compression { get; set; } = TextureCompression.Bc7;

    /// <summary>
    /// Keeps the image as a mask of numbers rather than colors, such as a painted sky: linear, 16 bits a channel as read (half
    /// floats), one level, its other settings left out. A grey image fills only red, with alpha 1.
    /// </summary>
    public bool Mask { get; set; }

    /// <summary>The largest width or height the texture keeps; a larger image is halved until it fits. 0 keeps its size.</summary>
    [Range(0, 16384)]
    public int MaxSize { get; set; }

    /// <summary>Multiplies the image's colour.</summary>
    [Color]
    public Vector3 Tint { get; set; } = Vector3.One;

    /// <summary>The left edge of the part of the image kept, in pixels.</summary>
    [Range(0, 16384)]
    public int CropX { get; set; }

    /// <summary>The top edge of the part of the image kept, in pixels.</summary>
    [Range(0, 16384)]
    public int CropY { get; set; }

    /// <summary>The width of the part kept; 0 keeps everything right of its left edge.</summary>
    [Range(0, 16384)]
    public int CropWidth { get; set; }

    /// <summary>The height of the part kept; 0 keeps everything below its top edge.</summary>
    [Range(0, 16384)]
    public int CropHeight { get; set; }

    /// <summary>The settings file of an image.</summary>
    public static string GetPath(string imagePath) => imagePath + Extension;
}
