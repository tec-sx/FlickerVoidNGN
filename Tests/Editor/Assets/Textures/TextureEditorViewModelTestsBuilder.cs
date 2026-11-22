using FlickerVoid.Editor.Assets.Textures;

namespace FlickerVoid.Editor.Assets.Tests.Textures;

/// <summary>
/// Builds a texture editor over a temporary content folder holding images written as uncompressed TGA files, with the
/// editor's unsaved content; sources are the content folder's own files.
/// </summary>
internal sealed class TextureEditorViewModelTestsBuilder : IDisposable
{
    private readonly UnsavedContentTestsBuilder _files = new();
    private UnsavedContent? _content;

    public UnsavedContent Content => _content ??= _files.Build();

    /// <summary>An image of the given size, its left half red and its right half blue.</summary>
    public TextureEditorViewModelTestsBuilder WithImage(string path, int width, int height)
    {
        byte[] tga = EncodeHalfRedHalfBlue(width, height);
        string fullPath = Path.Combine(_files.Folder, path);
        File.WriteAllBytes(fullPath, tga);
        return this;
    }

    public TextureEditorViewModelTestsBuilder WithFile(string path, string text)
    {
        _files.WithFile(path, text);
        return this;
    }

    public TextureEditorViewModel Build(string path) => new(Content, path, FindSource);

    public string ReadFile(string path) => _files.ReadFile(path);

    public bool Exists(string path) => File.Exists(Path.Combine(_files.Folder, path));

    /// <summary>The preview pixel's red, green and blue.</summary>
    public static (byte Red, byte Green, byte Blue) PixelAt(TextureEditorViewModel editor, int x, int y)
    {
        int index = (y * TextureEditorViewModel.PreviewSize + x) * 4;
        byte[] pixels = editor.PreviewPixels!;
        return (pixels[index], pixels[index + 1], pixels[index + 2]);
    }

    public void Dispose() => _files.Dispose();

    private string FindSource(string path) => Path.Combine(_files.Folder, path);

    /// <summary>An uncompressed 32-bit TGA, rows from the top.</summary>
    private static byte[] EncodeHalfRedHalfBlue(int width, int height)
    {
        const int headerSize = 18;
        byte[] tga = new byte[headerSize + width * height * 4];
        tga[2] = 2;
        tga[12] = (byte)width;
        tga[13] = (byte)(width >> 8);
        tga[14] = (byte)height;
        tga[15] = (byte)(height >> 8);
        tga[16] = 32;
        tga[17] = 0x28;
        for (int texel = 0; texel < width * height; texel++)
        {
            int destination = headerSize + texel * 4;
            bool left = texel % width < width / 2;
            if (left)
            {
                tga[destination + 2] = 255;
            }
            else
            {
                tga[destination] = 255;
            }

            tga[destination + 3] = 255;
        }

        return tga;
    }
}
