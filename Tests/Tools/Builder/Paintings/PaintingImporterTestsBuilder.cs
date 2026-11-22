using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Tools.Builder.Paintings;

namespace FlickerVoid.Tools.Builder.Tests.Paintings;

/// <summary>
/// Builds an asset builder with the painting importer under test, over new temporary source and output folders that are
/// deleted when disposed, and writes Rebelle paintings as Rebelle does: a zip of <c>artwork.xml</c>, listing the top
/// layer first, and one PNG per layer cropped to its paint, placed by an <c>offset</c> text chunk.
/// </summary>
internal sealed class PaintingImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();
    private readonly List<(string Xml, string FileName, byte[] Image)> _layers = [];
    private readonly Dictionary<string, byte[]> _structures = [];
    private int _width = 2;
    private int _height = 1;
    private string _canvas = "width='2.000000' height='1.000000'";

    /// <summary>The canvas's size in texels, written as Rebelle writes it, or as given in <paramref name="written"/>.</summary>
    public PaintingImporterTestsBuilder WithCanvas(int width, int height, string? written = null)
    {
        _width = width;
        _height = height;
        _canvas = written ?? $"width='{width}.000000' height='{height}.000000'";
        return this;
    }

    /// <summary>Adds a layer above the ones added before: straight-alpha RGBA texels in a box at the given place on the canvas.</summary>
    public PaintingImporterTestsBuilder WithLayer(byte[] rgba, float opacity = 1, bool visible = true, int x = 0, int y = 0, int width = 0, int height = 0)
    {
        if (width == 0)
        {
            width = _width;
            height = _height;
        }

        string fileName = $"layer{_layers.Count}";
        string visibility = visible ? "1" : "0";
        string opacityText = opacity.ToString(CultureInfo.InvariantCulture);
        string xml = $"<layer type='FLUID' width='{_width}' height='{_height}' opacity='{opacityText}' visibility='{visibility}' blending_mode='NORMAL' filename='{fileName}' name='Layer {_layers.Count + 1}'/>";
        byte[] image = EncodePng(width, height, rgba, $"{x} {y}");
        _layers.Add((xml, fileName, image));
        return this;
    }

    /// <summary>
    /// Gives the last layer added its impasto: 16-bit heights in a box at the given place on the canvas, written as Rebelle's
    /// <c>layerN_structure.dat</c> ("BBOX", the box's inclusive corners and the canvas size, "UI_2", the count, the heights, all big-endian).
    /// </summary>
    public PaintingImporterTestsBuilder WithRelief(ushort[] heights, int x = 0, int y = 0, int width = 0, int height = 0)
    {
        if (width == 0)
        {
            width = _width;
            height = _height;
        }

        using MemoryStream data = new();
        data.Write(Encoding.ASCII.GetBytes("BBOX"));
        WriteNumbers(data, [x, y, x + width - 1, y + height - 1, _width, _height]);
        data.Write(Encoding.ASCII.GetBytes("UI_2"));
        WriteNumbers(data, [heights.Length]);
        Span<byte> number = stackalloc byte[2];
        foreach (ushort value in heights)
        {
            BinaryPrimitives.WriteUInt16BigEndian(number, value);
            data.Write(number);
        }

        string fileName = _layers[^1].FileName;
        _structures[fileName] = data.ToArray();
        return this;
    }

    /// <summary>Writes the painting with the layers added so far as <paramref name="path"/>.</summary>
    public PaintingImporterTestsBuilder WritePainting(string path)
    {
        string fullPath = Path.Combine(_files.SourceDirectory, path);
        using FileStream file = File.Create(fullPath);
        using ZipArchive zip = new(file, ZipArchiveMode.Create);
        StringBuilder xml = new();
        xml.Append(CultureInfo.InvariantCulture, $"<?xml version='1.0' encoding='UTF-8'?><aquarelle_artwork version='724'><canvas {_canvas}/><layers active_pos='0'>");
        for (int index = _layers.Count - 1; index >= 0; index--)
        {
            xml.Append(_layers[index].Xml);
        }

        xml.Append("</layers></aquarelle_artwork>");
        WriteEntry(zip, "artwork.xml", Encoding.UTF8.GetBytes(xml.ToString()));
        foreach ((_, string fileName, byte[] image) in _layers)
        {
            WriteEntry(zip, $"{fileName}.png", image);
            byte[] structure = _structures.GetValueOrDefault(fileName) ?? EmptyStructure();
            WriteEntry(zip, $"{fileName}_structure.dat", structure);
        }

        return this;
    }

    public PaintingImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build() => new([new PaintingImporter()]);

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    public bool HasOutput(string path) => File.Exists(Path.Combine(_files.OutputDirectory, path));

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

    private static void WriteEntry(ZipArchive zip, string name, byte[] data)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name);
        using Stream stream = entry.Open();
        stream.Write(data);
    }

    /// <summary>A layer without impasto, as Rebelle writes it: "ZERO" and a size.</summary>
    private static byte[] EmptyStructure()
    {
        using MemoryStream data = new();
        data.Write(Encoding.ASCII.GetBytes("ZERO"));
        WriteNumbers(data, [0]);
        return data.ToArray();
    }

    private static void WriteNumbers(Stream stream, int[] numbers)
    {
        Span<byte> number = stackalloc byte[4];
        foreach (int value in numbers)
        {
            BinaryPrimitives.WriteInt32BigEndian(number, value);
            stream.Write(number);
        }
    }

    /// <summary>An 8-bit RGBA PNG with an <c>offset</c> text chunk, rows unfiltered.</summary>
    private static byte[] EncodePng(int width, int height, byte[] rgba, string offset)
    {
        using MemoryStream png = new();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        byte[] text = Encoding.Latin1.GetBytes($"offset\0{offset}");
        WriteChunk(png, "tEXt", text);
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            int rowSize = width * 4;
            for (int row = 0; row < height; row++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba, row * rowSize, rowSize);
            }
        }

        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        png.Write(number);
        byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
        png.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeAndData));
        png.Write(number);
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
