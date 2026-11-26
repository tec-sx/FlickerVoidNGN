using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// The images the asset browser's tiles show: decoded small once per file and kept while the file is unchanged, so scrolling a
/// folder of textures reads each file once.
/// </summary>
public static class AssetThumbnails
{
    /// <summary>How wide a thumbnail is decoded, in pixels: the tile's 64 at twice the scale.</summary>
    private const int DecodeWidth = 128;

    private static readonly Dictionary<string, (DateTime Written, Bitmap? Image)> s_cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A tile's thumbnail from its file's full path, or null when there is none or it can't be read.</summary>
    public static FuncValueConverter<string?, Bitmap?> Image { get; } = new(Load);

    private static Bitmap? Load(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        DateTime written = File.GetLastWriteTimeUtc(path);
        if (s_cache.TryGetValue(path, out (DateTime Written, Bitmap? Image) cached) && cached.Written == written)
        {
            return cached.Image;
        }

        Bitmap? image = null;
        try
        {
            using FileStream stream = File.OpenRead(path);
            image = Bitmap.DecodeToWidth(stream, DecodeWidth);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            image = null;
        }

        s_cache[path] = (written, image);
        return image;
    }
}
