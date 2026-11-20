using System.Numerics;

using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class IrradianceProbesTests
{
    private readonly IrradianceProbesTestsBuilder _builder = new();

    [Fact]
    public void CountsProbesAlongXFirst()
    {
        IrradianceProbes probes = _builder.WithCounts(3, 2, 4).Build();

        probes.GetIndex(1, 0, 0).Should().Be(1);
        probes.GetIndex(0, 1, 0).Should().Be(3);
        probes.GetIndex(0, 0, 1).Should().Be(6);
        probes.Probes.Length.Should().Be(24);
    }

    [Fact]
    public void ReadsBackWhatItSaved()
    {
        IrradianceProbes probes = _builder.WithCounts(2, 1, 2).WithEvenIrradiance(new Vector3(0.1f, 0.2f, 0.3f)).Build();

        IrradianceProbes loaded = IrradianceProbesTestsBuilder.RoundTrip(probes);

        loaded.CountX.Should().Be(2);
        loaded.CountY.Should().Be(1);
        loaded.CountZ.Should().Be(2);
        loaded.Probes.ToArray().Should().Equal(probes.Probes.ToArray());
    }

    [Fact]
    public void RejectsFilesThatArentProbes()
    {
        using MemoryStream stream = new([1, 2, 3, 4, 5, 6, 7, 8]);

        Action load = () => IrradianceProbes.Load(stream);

        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void PutsAProbeInTheMiddleOfEachEqualCell()
    {
        IrradianceVolume volume = new() { Size = new Vector3(4, 2, 6), ProbesX = 3, ProbesY = 2, ProbesZ = 4 };
        Vector3 center = new(10, 0, 0);

        Vector3 first = volume.GetProbePosition(center, 0, 0, 0);
        Vector3 last = volume.GetProbePosition(center, 2, 1, 3);
        Vector3 middle = volume.GetProbePosition(center, 1, 0, 0);

        first.X.Should().BeApproximately(8 + (4f / 6), 1e-5f);
        first.Y.Should().BeApproximately(-0.5f, 1e-5f);
        first.Z.Should().BeApproximately(-2.25f, 1e-5f);
        last.X.Should().BeApproximately(12 - (4f / 6), 1e-5f);
        last.Y.Should().BeApproximately(0.5f, 1e-5f);
        last.Z.Should().BeApproximately(2.25f, 1e-5f);
        middle.X.Should().BeApproximately(10, 1e-5f);
    }

    [Fact]
    public void PutsALoneProbeInTheMiddle()
    {
        IrradianceVolume volume = new() { Size = new Vector3(4, 2, 6), ProbesX = 1, ProbesY = 1, ProbesZ = 1 };

        Vector3 position = volume.GetProbePosition(Vector3.Zero, 0, 0, 0);

        position.Should().Be(Vector3.Zero);
    }
}
