using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

using StbImageSharp;

namespace FlickerVoid.Editor.Assets.Textures;

/// <summary>
/// The texture editor: a field for each setting of an image (its <c>.fvtexture</c> beside it: compression, size limit, tint,
/// crop) or of a canvas (<c>.fvcanvas</c>: image, tiling, tint, grain, curvature), held unsaved as it changes until saved or
/// discarded, and a preview drawn on the CPU of what the build makes of the image, or of the canvas repeated by its tiling.
/// </summary>
public sealed partial class TextureEditorViewModel : ObservableObject
{
    public const int PreviewSize = 256;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly UnsavedContent _content;
    private readonly Func<string, string> _findSource;
    private readonly FindContentFiles? _files;
    private readonly string _settingsPath;
    private object _settings = null!;

    /// <param name="findSource">The full path of a content file's source, which the preview decodes.</param>
    public TextureEditorViewModel(UnsavedContent content, string path, Func<string, string> findSource, FindContentFiles? files = null)
    {
        _content = content;
        _findSource = findSource;
        _files = files;
        Path = path;
        IsCanvas = path.EndsWith(".fvcanvas", StringComparison.OrdinalIgnoreCase);
        if (IsCanvas)
        {
            _settingsPath = path;
        }
        else
        {
            _settingsPath = TextureSettings.GetPath(path);
        }

        Load();
    }

    /// <summary>The image or canvas being edited.</summary>
    public string Path { get; }

    public bool IsCanvas { get; }

    public string Heading => System.IO.Path.GetFileName(Path);

    [ObservableProperty]
    public partial IReadOnlyList<FieldViewModel> Fields { get; set; } = [];

    /// <summary>The preview as Rgba8 pixels, <see cref="PreviewSize"/> square, or null with nothing to show.</summary>
    [ObservableProperty]
    public partial byte[]? PreviewPixels { get; set; }

    /// <summary>What the build makes of an image (its size and compression), or a canvas's image.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    /// <summary>Why there is no preview, if there isn't.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [RelayCommand]
    public void Save()
    {
        _content.Save(_settingsPath);
    }

    [RelayCommand]
    public void Discard()
    {
        _content.Discard(_settingsPath);
        Load();
    }

    private void Load()
    {
        if (IsCanvas)
        {
            _settings = Read(RenderingJsonContext.Default.CanvasFile) ?? new CanvasFile();
            Fields = CreateFields(RenderingJsonContext.Default.CanvasFile);
        }
        else
        {
            _settings = Read(RenderingJsonContext.Default.TextureSettings) ?? new TextureSettings();
            Fields = CreateFields(RenderingJsonContext.Default.TextureSettings);
        }

        ShowPreview();
    }

    private T? Read<T>(JsonTypeInfo<T> typeInfo)
        where T : class
    {
        byte[] contents;
        try
        {
            contents = _content.Read(_settingsPath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        return JsonSerializer.Deserialize(contents, typeInfo);
    }

    private List<FieldViewModel> CreateFields<T>(JsonTypeInfo<T> typeInfo)
    {
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in typeInfo.Properties)
        {
            if (info.Name == JsonSchemas.SchemaProperty || info.Get is null)
            {
                continue;
            }

            ObjectProperty property = new(_settings, info, HandleEdited);
            FieldViewModel field = FieldFactory.Create(property, _files);
            fields.Add(field);
        }

        return fields;
    }

    private void HandleEdited()
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            if (_settings is CanvasFile canvas)
            {
                JsonSerializer.Serialize(writer, canvas, RenderingJsonContext.Default.CanvasFile);
            }
            else
            {
                JsonSerializer.Serialize(writer, (TextureSettings)_settings, RenderingJsonContext.Default.TextureSettings);
            }
        }

        byte[] contents = stream.ToArray();
        _content.Edit(_settingsPath, contents);
        ShowPreview();
    }

    private void ShowPreview()
    {
        string? image = Path;
        if (_settings is CanvasFile canvas)
        {
            image = canvas.Image;
        }

        TextureEdits? decoded = Decode(image);
        if (decoded is not { } source)
        {
            PreviewPixels = null;
            Summary = "";
            return;
        }

        Status = "";
        if (_settings is CanvasFile file)
        {
            TextureEdits tinted = Intensify(source, file.Intensity).Tint(file.Tint);
            Summary = $"{image}, {source.Width} × {source.Height}";
            PreviewPixels = Tile(tinted, file.Tiling);
            return;
        }

        TextureSettings settings = (TextureSettings)_settings;
        TextureEdits edited = source.Apply(settings);
        string compression = settings.Compression == TextureCompression.Bc7 ? "BC7" : "uncompressed";
        Summary = $"{edited.Width} × {edited.Height}, {compression}";
        PreviewPixels = Fit(edited);
    }

    /// <summary>The texels moved toward or away from their average colour, as the canvas's intensity does in the game.</summary>
    private static TextureEdits Intensify(TextureEdits image, float intensity)
    {
        if (intensity == 1)
        {
            return image;
        }

        int texels = image.Width * image.Height;
        Vector3 sum = Vector3.Zero;
        for (int texel = 0; texel < texels; texel++)
        {
            int offset = texel * 4;
            sum += new Vector3(image.Rgba[offset], image.Rgba[offset + 1], image.Rgba[offset + 2]);
        }

        Vector3 average = sum / texels;
        byte[] rgba = new byte[image.Rgba.Length];
        for (int texel = 0; texel < texels; texel++)
        {
            int offset = texel * 4;
            for (int channel = 0; channel < 3; channel++)
            {
                float value = image.Rgba[offset + channel];
                float moved = float.Lerp(average[channel], value, intensity);
                rgba[offset + channel] = (byte)Math.Clamp(MathF.Round(moved), 0, 255);
            }

            rgba[offset + 3] = image.Rgba[offset + 3];
        }

        return new TextureEdits(rgba, image.Width, image.Height);
    }

    /// <summary>The image's texels, or null when it can't be read, saying why.</summary>
    private TextureEdits? Decode(string? path)
    {
        if (path is null)
        {
            Status = "No image to show.";
            return null;
        }

        if (path.EndsWith(".reb", StringComparison.OrdinalIgnoreCase))
        {
            Status = "Paintings have no preview yet; the viewport shows them.";
            return null;
        }

        string fullPath = _findSource(path);
        try
        {
            byte[] encoded = File.ReadAllBytes(fullPath);
            ImageResult result = ImageResult.FromMemory(encoded, ColorComponents.RedGreenBlueAlpha);
            return new TextureEdits(result.Data, result.Width, result.Height);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            Status = $"Can't read {path}: {exception.Message}";
            return null;
        }
    }

    /// <summary>The texels scaled to fit the preview, centred, transparent around them.</summary>
    private static byte[] Fit(TextureEdits image)
    {
        byte[] pixels = new byte[PreviewSize * PreviewSize * 4];
        float scale = (float)PreviewSize / Math.Max(image.Width, image.Height);
        int width = Math.Max((int)(image.Width * scale), 1);
        int height = Math.Max((int)(image.Height * scale), 1);
        int left = (PreviewSize - width) / 2;
        int top = (PreviewSize - height) / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceX = Math.Min((int)(x / scale), image.Width - 1);
                int sourceY = Math.Min((int)(y / scale), image.Height - 1);
                CopyTexel(image, sourceX, sourceY, pixels, left + x, top + y);
            }
        }

        return pixels;
    }

    /// <summary>The texels repeated the tiling's times down the preview, and as often across as their shape allows.</summary>
    private static byte[] Tile(TextureEdits image, float tiling)
    {
        byte[] pixels = new byte[PreviewSize * PreviewSize * 4];
        float tileHeight = PreviewSize / Math.Max(tiling, 0.01f);
        float tileWidth = tileHeight * image.Width / image.Height;
        Vector2 texelsPerPixel = new(image.Width / tileWidth, image.Height / tileHeight);
        for (int y = 0; y < PreviewSize; y++)
        {
            for (int x = 0; x < PreviewSize; x++)
            {
                int sourceX = (int)(x * texelsPerPixel.X) % image.Width;
                int sourceY = (int)(y * texelsPerPixel.Y) % image.Height;
                CopyTexel(image, sourceX, sourceY, pixels, x, y);
            }
        }

        return pixels;
    }

    private static void CopyTexel(TextureEdits image, int sourceX, int sourceY, byte[] pixels, int x, int y)
    {
        int source = (sourceY * image.Width + sourceX) * 4;
        int destination = (y * PreviewSize + x) * 4;
        Array.Copy(image.Rgba, source, pixels, destination, 4);
    }
}
