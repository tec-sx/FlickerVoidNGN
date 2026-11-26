using System.Numerics;
using System.Text;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds a grid of probes, dark and seeing nothing near unless given an even irradiance or distances.</summary>
internal sealed class IrradianceProbesTestsBuilder
{
    private int _countX = 2;
    private int _countY = 2;
    private int _countZ = 2;
    private Vector3 _irradiance;
    private Vector2? _distances;

    public IrradianceProbesTestsBuilder WithCounts(int x, int y, int z)
    {
        _countX = x;
        _countY = y;
        _countZ = z;
        return this;
    }

    public IrradianceProbesTestsBuilder WithEvenIrradiance(Vector3 irradiance)
    {
        _irradiance = irradiance;
        return this;
    }

    /// <summary>The same mean and mean square distance in every texel of every probe's map.</summary>
    public IrradianceProbesTestsBuilder WithEvenDistances(Vector2 distances)
    {
        _distances = distances;
        return this;
    }

    public IrradianceProbes Build()
    {
        IrradianceProbes probes = new(_countX, _countY, _countZ);
        IrradianceProbe even = IrradianceProbe.Even(_irradiance);
        probes.Probes.Fill(even);
        if (_distances is { } distances)
        {
            probes.Distances.Fill(distances);
        }

        return probes;
    }

    /// <summary>A file as the first version wrote it: the header and one probe's light, with no distances.</summary>
    public static MemoryStream FirstVersionFile()
    {
        MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x52505646u);
            writer.Write(1u);
            writer.Write(1);
            writer.Write(1);
            writer.Write(1);
            writer.Write(new byte[48]);
        }

        stream.Position = 0;
        return stream;
    }

    public static IrradianceProbes RoundTrip(IrradianceProbes probes)
    {
        using MemoryStream stream = new();
        probes.Save(stream);
        stream.Position = 0;
        return IrradianceProbes.Load(stream);
    }
}
