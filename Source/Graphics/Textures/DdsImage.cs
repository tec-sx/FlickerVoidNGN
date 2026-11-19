using System.Buffers.Binary;

namespace FlickerVoid.Engine.Graphics.Textures;

/// <summary>
/// A 2D texture in a DDS file with the DX10 header: its size, format, and every mip level's tightly packed rows, the full
/// size first. The content builder writes textures this way and the game uploads them as they are.
/// </summary>
public sealed class DdsImage
{
    private const uint Magic = 0x20534444;
    private const int HeaderSize = 124;
    private const int PixelFormatSize = 32;
    private const int Dx10HeaderSize = 20;
    private const uint Dx10FourCc = 0x30315844;
    private const uint FourCcFlag = 0x4;
    private const uint TextureFlags = 0x1007;
    private const uint MipMapCountFlag = 0x20000;
    private const uint LinearSizeFlag = 0x80000;
    private const uint PitchFlag = 0x8;
    private const uint TextureCaps = 0x1000;
    private const uint MipMapCaps = 0x400008;
    private const uint Texture2D = 3;

    public DdsImage(int width, int height, PixelFormat format, int mipLevels, byte[] data)
    {
        Width = width;
        Height = height;
        Format = format;
        MipLevels = mipLevels;
        Data = data;
        TextureDescription description = Describe(TextureUsage.None);
        int expected = description.GetDataSize();
        if (data.Length != expected)
        {
            throw new ArgumentException($"A {width}x{height} {format} image with {mipLevels} levels takes {expected} bytes, not {data.Length}.", nameof(data));
        }
    }

    public int Width { get; }

    public int Height { get; }

    public PixelFormat Format { get; }

    public int MipLevels { get; }

    public byte[] Data { get; }

    /// <summary>Reads a DDS file with a DX10 header holding one 2D texture.</summary>
    public static DdsImage Read(Stream stream)
    {
        byte[] header = new byte[4 + HeaderSize + Dx10HeaderSize];
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (read < header.Length || ReadUInt(header, 0) != Magic || ReadUInt(header, 4) != HeaderSize)
        {
            throw new InvalidDataException("Not a DDS file.");
        }

        int pixelFormat = 4 + 72;
        bool hasDx10Header = (ReadUInt(header, pixelFormat + 4) & FourCcFlag) != 0 && ReadUInt(header, pixelFormat + 8) == Dx10FourCc;
        int dx10 = 4 + HeaderSize;
        if (!hasDx10Header || ReadUInt(header, dx10 + 4) != Texture2D || ReadUInt(header, dx10 + 12) != 1)
        {
            throw new InvalidDataException("Only DDS files with a DX10 header holding one 2D texture are read.");
        }

        PixelFormat format = (PixelFormat)ReadUInt(header, dx10);
        if (!Enum.IsDefined(format) || format.IsDepth())
        {
            throw new InvalidDataException($"The DDS format {(int)format} isn't a texture format the engine has.");
        }

        int height = (int)ReadUInt(header, 12);
        int width = (int)ReadUInt(header, 16);
        int mipLevels = Math.Max(1, (int)ReadUInt(header, 28));
        TextureDescription description = new(width, height, format, TextureUsage.None) { MipLevels = mipLevels };
        byte[] data = new byte[description.GetDataSize()];
        stream.ReadExactly(data);
        return new DdsImage(width, height, format, mipLevels, data);
    }

    public void Write(Stream stream)
    {
        byte[] header = new byte[4 + HeaderSize + Dx10HeaderSize];
        bool compressed = Format.IsBlockCompressed();
        uint flags = TextureFlags | MipMapCountFlag;
        if (compressed)
        {
            flags |= LinearSizeFlag;
        }
        else
        {
            flags |= PitchFlag;
        }

        uint caps = TextureCaps;
        if (MipLevels > 1)
        {
            caps |= MipMapCaps;
        }

        TextureDescription description = Describe(TextureUsage.None);
        int pitchOrLinearSize = Format.GetRowSize(Width);
        if (compressed)
        {
            pitchOrLinearSize = description.GetLevelSize(0);
        }

        WriteUInt(header, 0, Magic);
        WriteUInt(header, 4, HeaderSize);
        WriteUInt(header, 8, flags);
        WriteUInt(header, 12, (uint)Height);
        WriteUInt(header, 16, (uint)Width);
        WriteUInt(header, 20, (uint)pitchOrLinearSize);
        WriteUInt(header, 28, (uint)MipLevels);
        int pixelFormat = 4 + 72;
        WriteUInt(header, pixelFormat, PixelFormatSize);
        WriteUInt(header, pixelFormat + 4, FourCcFlag);
        WriteUInt(header, pixelFormat + 8, Dx10FourCc);
        WriteUInt(header, 4 + 104, caps);
        int dx10 = 4 + HeaderSize;
        WriteUInt(header, dx10, (uint)Format);
        WriteUInt(header, dx10 + 4, Texture2D);
        WriteUInt(header, dx10 + 12, 1);
        stream.Write(header);
        stream.Write(Data);
    }

    /// <summary>A texture description of this image's size, format and levels, for the given use.</summary>
    public TextureDescription Describe(TextureUsage usage) => new(Width, Height, Format, usage) { MipLevels = MipLevels };

    private static uint ReadUInt(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    private static void WriteUInt(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
