using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds a grid of probes, dark unless given an even irradiance.</summary>
internal sealed class IrradianceProbesTestsBuilder
{
    private int _countX = 2;
    private int _countY = 2;
    private int _countZ = 2;
    private Vector3 _irradiance;

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

    public IrradianceProbes Build()
    {
        IrradianceProbes probes = new(_countX, _countY, _countZ);
        IrradianceProbe even = IrradianceProbe.Even(_irradiance);
        probes.Probes.Fill(even);
        return probes;
    }

    public static IrradianceProbes RoundTrip(IrradianceProbes probes)
    {
        using MemoryStream stream = new();
        probes.Save(stream);
        stream.Position = 0;
        return IrradianceProbes.Load(stream);
    }
}
