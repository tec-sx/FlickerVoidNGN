using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Engine.Editor.Tests.Scenes;

public sealed class SceneProbesTests : IDisposable
{
    private readonly SceneProbesTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void BakesEachVolumeIntoItsProbesFile()
    {
        _builder.WithGreenGlowingCube().WithVolume("Hall", "Hall.probes");

        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().Equal("Hall.probes");
        IrradianceProbes probes = _builder.LoadProbes("Hall.probes");
        probes.CountX.Should().Be(SceneProbesTestsBuilder.ProbesPerAxis);
        IrradianceProbe probe = probes.Probes[0];
        probe.Green.X.Should().BeGreaterThan(probe.Red.X + 0.05f, "the probes see the cube glowing green");
    }

    [Fact]
    public void NamesTheProbesFileOfAVolumeWithoutOneAfterTheSceneAndTheVolume()
    {
        _builder.WithVolume("Hall", probes: null);

        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().Equal("Untitled.Hall.probes");
        _builder.VolumeProbes("Hall").Should().Be("Untitled.Hall.probes");
        _builder.LoadProbes("Untitled.Hall.probes").CountY.Should().Be(SceneProbesTestsBuilder.ProbesPerAxis);
    }

    [Fact]
    public void BakesNothingWithoutVolumes()
    {
        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().BeEmpty();
    }
}
