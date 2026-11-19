using System.Runtime.InteropServices;
using System.Text;

namespace FlickerVoid.Engine.Rendering;

/// <summary>A baked grid of irradiance probes for an <see cref="IrradianceVolume"/>, with its binary file format.</summary>
public sealed class IrradianceProbes
{
    private const uint Magic = 0x52505646; // "FVPR"
    private const uint Version = 1;

    private readonly IrradianceProbe[] _probes;

    public IrradianceProbes(int countX, int countY, int countZ)
    {
        CountX = countX;
        CountY = countY;
        CountZ = countZ;
        _probes = new IrradianceProbe[countX * countY * countZ];
    }

    public int CountX { get; }

    public int CountY { get; }

    public int CountZ { get; }

    /// <summary>Every probe, x changing fastest, then y, then z.</summary>
    public Span<IrradianceProbe> Probes => _probes;

    public int GetIndex(int x, int y, int z) => (((z * CountY) + y) * CountX) + x;

    public void Save(Stream stream)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(CountX);
        writer.Write(CountY);
        writer.Write(CountZ);
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(_probes.AsSpan());
        writer.Write(bytes);
    }

    public static IrradianceProbes Load(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        if (magic != Magic || version != Version)
        {
            throw new InvalidDataException("The file isn't a version 1 FlickerVoid irradiance probes file.");
        }

        int countX = reader.ReadInt32();
        int countY = reader.ReadInt32();
        int countZ = reader.ReadInt32();
        IrradianceProbes probes = new(countX, countY, countZ);
        Span<byte> bytes = MemoryMarshal.AsBytes(probes.Probes);
        reader.BaseStream.ReadExactly(bytes);
        return probes;
    }
}
