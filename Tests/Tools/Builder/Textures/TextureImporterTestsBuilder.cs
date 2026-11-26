using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

using FlickerVoid.Tools.Builder.Textures;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Tools.Builder.Tests.Textures;

/// <summary>
/// Builds an asset builder with the texture importer under test, over new temporary source and output folders that are
/// deleted when disposed, and writes images as uncompressed TGA files.
/// </summary>
internal sealed class TextureImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();

    public TextureImporterTestsBuilder WithImage(string path, int width, int height, byte[] rgba)
    {
        byte[] tga = EncodeTga(width, height, rgba);
        string fullPath = Path.Combine(_files.SourceDirectory, path);
        File.WriteAllBytes(fullPath, tga);
        return this;
    }

    /// <summary>Writes a 16-bit greyscale PNG, rows from the top.</summary>
    public TextureImporterTestsBuilder WithGreyPng16(string path, int width, int height, ushort[] greys)
    {
        byte[] png = EncodeGreyPng16(width, height, greys);
        string fullPath = Path.Combine(_files.SourceDirectory, path);
        File.WriteAllBytes(fullPath, png);
        return this;
    }

    public TextureImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build() => new([new TextureImporter()]);

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    public DdsImage ReadOutput(string path)
    {
        string fullPath = Path.Combine(_files.OutputDirectory, path);
        using FileStream stream = File.OpenRead(fullPath);
        return DdsImage.Read(stream);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    /// <summary>An uncompressed 32-bit TGA, rows from the top.</summary>
    private static byte[] EncodeTga(int width, int height, byte[] rgba)
    {
        const int headerSize = 18;
        byte[] tga = new byte[headerSize + rgba.Length];
        tga[2] = 2;
        tga[12] = (byte)width;
        tga[13] = (byte)(width >> 8);
        tga[14] = (byte)height;
        tga[15] = (byte)(height >> 8);
        tga[16] = 32;
        tga[17] = 0x28;
        for (int texel = 0; texel < rgba.Length / 4; texel++)
        {
            int source = texel * 4;
            int destination = headerSize + source;
            tga[destination] = rgba[source + 2];
            tga[destination + 1] = rgba[source + 1];
            tga[destination + 2] = rgba[source];
            tga[destination + 3] = rgba[source + 3];
        }

        return tga;
    }

    /// <summary>A 16-bit greyscale PNG: every row unfiltered, its samples big-endian.</summary>
    private static byte[] EncodeGreyPng16(int width, int height, ushort[] greys)
    {
        using MemoryStream raw = new();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < width; x++)
            {
                ushort grey = greys[(y * width) + x];
                raw.WriteByte((byte)(grey >> 8));
                raw.WriteByte((byte)grey);
            }
        }

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 16;
        using MemoryStream png = new();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
        stream.Write(typeAndData);
        byte[] crc = new byte[4];
        uint checksum = Crc32(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(crc, checksum);
        stream.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                uint mask = (uint)-(int)(crc & 1);
                crc = (crc >> 1) ^ (0xEDB88320 & mask);
            }
        }

        return ~crc;
    }
}
