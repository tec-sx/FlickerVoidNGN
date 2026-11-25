using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>
/// A grid of irradiance probes for an <see cref="IrradianceVolume"/>: each probe's light, and how far it sees in each direction,
/// which keeps its light from passing through walls; baked by <see cref="IrradianceBaker"/> into its binary file format, or traced
/// live (<see cref="SceneRenderer.LiveProbes"/>), which hold the same data.
/// </summary>
public sealed class IrradianceProbes
{
    /// <summary>The width and height of each probe's octahedral map of distances (Shaders/Probes.hlsl).</summary>
    public const int DistanceSize = 16;

    private const int DistanceTexels = DistanceSize * DistanceSize;
    private const uint Magic = 0x52505646; // "FVPR"
    private const uint Version = 2;

    private readonly IrradianceProbe[] _probes;
    private readonly Vector2[] _distances;

    /// <summary>Creates dark probes that see nothing within their reach.</summary>
    public IrradianceProbes(int countX, int countY, int countZ)
    {
        CountX = countX;
        CountY = countY;
        CountZ = countZ;
        int count = countX * countY * countZ;
        _probes = new IrradianceProbe[count];
        _distances = new Vector2[count * DistanceTexels];
        _distances.AsSpan().Fill(Vector2.One);
    }

    public int CountX { get; }

    public int CountY { get; }

    public int CountZ { get; }

    /// <summary>Every probe, x changing fastest, then y, then z.</summary>
    public Span<IrradianceProbe> Probes => _probes;

    /// <summary>
    /// Each probe's octahedral map of how far it sees, in the probes' order, <see cref="DistanceSize"/> rows of texels each (folded
    /// as the G-buffer folds normals): the mean distance and the mean squared distance of the texel's directions, as shares of the
    /// probe's reach, 1.5 times the diagonal of the cells between probes; 1 where nothing lies within it.
    /// </summary>
    public Span<Vector2> Distances => _distances;

    /// <summary>How many bytes <see cref="WriteTo"/> writes: every probe's light, then every probe's distances.</summary>
    public int ByteCount => (_probes.Length * Marshal.SizeOf<IrradianceProbe>()) + (_distances.Length * Marshal.SizeOf<Vector2>());

    public int GetIndex(int x, int y, int z) => (((z * CountY) + y) * CountX) + x;

    /// <summary>Copies every probe's light, then every probe's distances, as the GPU reads them.</summary>
    public void WriteTo(Span<byte> destination)
    {
        ReadOnlySpan<byte> light = MemoryMarshal.AsBytes(_probes.AsSpan());
        ReadOnlySpan<byte> distances = MemoryMarshal.AsBytes(_distances.AsSpan());
        light.CopyTo(destination);
        Span<byte> after = destination[light.Length..];
        distances.CopyTo(after);
    }

    /// <summary>Takes every probe's light, then every probe's distances, as <see cref="WriteTo"/> lays them out.</summary>
    public void ReadFrom(ReadOnlySpan<byte> source)
    {
        Span<byte> light = MemoryMarshal.AsBytes(_probes.AsSpan());
        Span<byte> distances = MemoryMarshal.AsBytes(_distances.AsSpan());
        source[..light.Length].CopyTo(light);
        ReadOnlySpan<byte> after = source[light.Length..];
        after[..distances.Length].CopyTo(distances);
    }

    public void Save(Stream stream)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(CountX);
        writer.Write(CountY);
        writer.Write(CountZ);
        byte[] bytes = new byte[ByteCount];
        WriteTo(bytes);
        writer.Write(bytes);
    }

    public static IrradianceProbes Load(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException("The file isn't a FlickerVoid irradiance probes file.");
        }

        if (version != Version)
        {
            throw new InvalidDataException($"The probes file is version {version}, which knows nothing of the probes' distances; bake it again.");
        }

        int countX = reader.ReadInt32();
        int countY = reader.ReadInt32();
        int countZ = reader.ReadInt32();
        IrradianceProbes probes = new(countX, countY, countZ);
        byte[] bytes = new byte[probes.ByteCount];
        reader.BaseStream.ReadExactly(bytes);
        probes.ReadFrom(bytes);
        return probes;
    }
}
