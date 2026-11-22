using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

using StbImageSharp;

namespace FlickerVoid.Tools.Builder.Paintings;

/// <summary>
/// Reads a Rebelle painting (<c>.reb</c>): a zip of <c>artwork.xml</c>, which gives the canvas size and lists the layers
/// top first, and a PNG per layer holding its paint in straight-alpha RGBA, cropped to where it was painted and placed on
/// the canvas by an <c>offset</c> text chunk ("x y"). Alpha is how much paint lies there. A layer's impasto, how high its
/// paint stands, is in <c>layerN_structure.dat</c>: "BBOX", six big-endian uint32s (the box's inclusive corners x0 y0 x1 y1,
/// then the canvas's width and height), "UI_2", a uint32 count and that many big-endian uint16 heights; a layer without
/// impasto writes "ZERO" instead.
/// </summary>
public sealed class RebellePainting : IDisposable
{
    private const string ArtworkEntry = "artwork.xml";
    private const string OffsetKey = "offset";

    /// <summary>The impasto height the relief's brightest texel stands for; thick oils reach about 5000.</summary>
    public const float ReliefRange = 8192;

    private readonly ZipArchive _zip;

    private RebellePainting(ZipArchive zip, int width, int height, IReadOnlyList<PaintingLayer> layers)
    {
        _zip = zip;
        Width = width;
        Height = height;
        Layers = layers;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The layers from the bottom up.</summary>
    public IReadOnlyList<PaintingLayer> Layers { get; }

    public static RebellePainting Open(Stream stream)
    {
        ZipArchive zip = new(stream, ZipArchiveMode.Read);
        ZipArchiveEntry artworkEntry = zip.GetEntry(ArtworkEntry) ?? throw new InvalidDataException("The painting has no artwork.xml.");
        XDocument artwork;
        using (Stream artworkStream = artworkEntry.Open())
        {
            artwork = XDocument.Load(artworkStream);
        }

        XElement root = artwork.Root ?? throw new InvalidDataException("The painting's artwork.xml is empty.");
        XElement canvas = root.Element("canvas") ?? throw new InvalidDataException("The painting's artwork.xml has no canvas.");
        // Canvases sized in print units have fractional sizes (2480.314941 by 3507.874023 for A4 at 300 dpi); the layers round them.
        float canvasWidth = ReadNumber(canvas, "width");
        float canvasHeight = ReadNumber(canvas, "height");
        int width = (int)MathF.Round(canvasWidth);
        int height = (int)MathF.Round(canvasHeight);
        List<PaintingLayer> layers = [];
        XElement? layerList = root.Element("layers");
        if (layerList is not null)
        {
            foreach (XElement layer in layerList.Elements("layer"))
            {
                string fileName = (string?)layer.Attribute("filename") ?? throw new InvalidDataException("A layer of the painting names no file.");
                float opacity = ReadNumber(layer, "opacity");
                bool visible = (string?)layer.Attribute("visibility") != "0";
                layers.Add(new PaintingLayer(fileName, opacity, visible));
            }
        }

        layers.Reverse();
        return new RebellePainting(zip, width, height, layers);
    }

    /// <summary>Lays the visible layers over each other at their opacity: straight-alpha RGBA of the whole canvas, rows from the top.</summary>
    public byte[] Composite()
    {
        float[] canvas = new float[Width * Height * 4];
        foreach (PaintingLayer layer in Layers)
        {
            if (layer.Visible && layer.Opacity > 0)
            {
                Paint(canvas, layer);
            }
        }

        byte[] rgba = new byte[canvas.Length];
        for (int index = 0; index < canvas.Length; index++)
        {
            float value = MathF.Round(canvas[index] * 255);
            rgba[index] = (byte)Math.Clamp(value, 0, 255);
        }

        return rgba;
    }

    /// <summary>
    /// Stacks the visible layers' impasto at their opacity, as a share of <see cref="ReliefRange"/> in grey RGBA of the whole
    /// canvas, rows from the top; null when no visible layer has any.
    /// </summary>
    public byte[]? CompositeRelief()
    {
        float[] heights = new float[Width * Height];
        bool raised = false;
        foreach (PaintingLayer layer in Layers)
        {
            if (layer.Visible && layer.Opacity > 0)
            {
                raised |= Raise(heights, layer);
            }
        }

        if (!raised)
        {
            return null;
        }

        byte[] rgba = new byte[heights.Length * 4];
        for (int index = 0; index < heights.Length; index++)
        {
            float value = MathF.Round(heights[index] / ReliefRange * 255);
            byte grey = (byte)Math.Clamp(value, 0, 255);
            rgba[index * 4] = grey;
            rgba[index * 4 + 1] = grey;
            rgba[index * 4 + 2] = grey;
            rgba[index * 4 + 3] = 255;
        }

        return rgba;
    }

    public void Dispose()
    {
        _zip.Dispose();
    }

    /// <summary>Adds the layer's impasto to the heights; false when it has none.</summary>
    private bool Raise(float[] heights, PaintingLayer layer)
    {
        ZipArchiveEntry? entry = _zip.GetEntry($"{layer.FileName}_structure.dat");
        if (entry is null)
        {
            return false;
        }

        byte[] data;
        using (Stream stream = entry.Open())
        using (MemoryStream memory = new())
        {
            stream.CopyTo(memory);
            data = memory.ToArray();
        }

        const int headerSize = 4 + 6 * 4 + 4 + 4;
        if (data.Length < headerSize || Encoding.ASCII.GetString(data, 0, 4) != "BBOX" || Encoding.ASCII.GetString(data, 28, 4) != "UI_2")
        {
            return false;
        }

        Span<byte> header = data.AsSpan(4);
        int x0 = BinaryPrimitives.ReadInt32BigEndian(header);
        int y0 = BinaryPrimitives.ReadInt32BigEndian(header[4..]);
        int x1 = BinaryPrimitives.ReadInt32BigEndian(header[8..]);
        int y1 = BinaryPrimitives.ReadInt32BigEndian(header[12..]);
        int count = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(32));
        int boxWidth = x1 - x0 + 1;
        int boxHeight = y1 - y0 + 1;
        if (boxWidth <= 0 || boxHeight <= 0 || count != boxWidth * boxHeight || data.Length < headerSize + count * 2)
        {
            throw new InvalidDataException($"The impasto of layer '{layer.FileName}' doesn't fit its box.");
        }

        for (int row = 0; row < boxHeight; row++)
        {
            int y = y0 + row;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            for (int column = 0; column < boxWidth; column++)
            {
                int x = x0 + column;
                if (x < 0 || x >= Width)
                {
                    continue;
                }

                int source = headerSize + (row * boxWidth + column) * 2;
                ushort height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(source));
                heights[y * Width + x] += height * layer.Opacity;
            }
        }

        return true;
    }

    private void Paint(float[] canvas, PaintingLayer layer)
    {
        ZipArchiveEntry entry = _zip.GetEntry($"{layer.FileName}.png") ?? throw new InvalidDataException($"The painting has no image for layer '{layer.FileName}'.");
        byte[] png;
        using (Stream stream = entry.Open())
        using (MemoryStream memory = new())
        {
            stream.CopyTo(memory);
            png = memory.ToArray();
        }

        (int offsetX, int offsetY) = ReadOffset(png);
        ImageResult image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        for (int row = 0; row < image.Height; row++)
        {
            int y = offsetY + row;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            for (int column = 0; column < image.Width; column++)
            {
                int x = offsetX + column;
                if (x < 0 || x >= Width)
                {
                    continue;
                }

                int source = (row * image.Width + column) * 4;
                int destination = (y * Width + x) * 4;
                Over(canvas, destination, image.Data, source, layer.Opacity);
            }
        }
    }

    /// <summary>Lays one straight-alpha texel over another (Porter-Duff over).</summary>
    private static void Over(float[] canvas, int destination, byte[] layer, int source, float opacity)
    {
        float alpha = layer[source + 3] / 255f * opacity;
        if (alpha <= 0)
        {
            return;
        }

        float below = canvas[destination + 3];
        float covered = alpha + below * (1 - alpha);
        for (int channel = 0; channel < 3; channel++)
        {
            float color = layer[source + channel] / 255f;
            float mixed = color * alpha + canvas[destination + channel] * below * (1 - alpha);
            canvas[destination + channel] = mixed / covered;
        }

        canvas[destination + 3] = covered;
    }

    /// <summary>The layer's place on the canvas from its PNG's <c>offset</c> text chunk, or the corner when it has none.</summary>
    private static (int X, int Y) ReadOffset(byte[] png)
    {
        const int signatureSize = 8;
        int position = signatureSize;
        while (position + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            string type = Encoding.ASCII.GetString(png, position + 4, 4);
            int dataStart = position + 8;
            if (type == "tEXt" && dataStart + length <= png.Length)
            {
                string text = Encoding.Latin1.GetString(png, dataStart, length);
                string[] keyAndValue = text.Split('\0', 2);
                if (keyAndValue.Length == 2 && keyAndValue[0] == OffsetKey)
                {
                    string[] numbers = keyAndValue[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int x = int.Parse(numbers[0], CultureInfo.InvariantCulture);
                    int y = int.Parse(numbers[1], CultureInfo.InvariantCulture);
                    return (x, y);
                }
            }

            if (type == "IDAT")
            {
                break;
            }

            position = dataStart + length + 4;
        }

        return (0, 0);
    }

    private static float ReadNumber(XElement element, string name)
    {
        string text = (string?)element.Attribute(name) ?? throw new InvalidDataException($"The painting's {element.Name} has no {name}.");
        return float.Parse(text, CultureInfo.InvariantCulture);
    }
}
