using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Engine.Rendering.GlobalIllumination;

namespace FlickerVoid.Editor.Tests.Scenes;

public sealed class SceneProbesTests : IDisposable
{
    private readonly SceneProbesTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void BakesEachVolumeIntoItsProbesFile()
    {
        _builder.WithGreenGlowingCube().WithVolume("Hall", "Hall.fvprobes");

        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().Equal("Hall.fvprobes");
        IrradianceProbes probes = _builder.LoadProbes("Hall.fvprobes");
        probes.CountX.Should().Be(SceneProbesTestsBuilder.ProbesPerAxis);
        IrradianceProbe probe = probes.Probes[0];
        probe.Green.X.Should().BeGreaterThan(probe.Red.X + 0.05f, "the probes see the cube glowing green");
    }

    [Fact]
    public void NamesTheProbesFileOfAVolumeWithoutOneAfterTheSceneAndTheVolume()
    {
        _builder.WithVolume("Hall", probes: null);

        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().Equal("Untitled.Hall.fvprobes");
        _builder.VolumeProbes("Hall").Should().Be("Untitled.Hall.fvprobes");
        _builder.LoadProbes("Untitled.Hall.fvprobes").CountY.Should().Be(SceneProbesTestsBuilder.ProbesPerAxis);
    }

    [Fact]
    public void BakesNothingWithoutVolumes()
    {
        IReadOnlyList<string> baked = _builder.Bake();

        baked.Should().BeEmpty();
    }
}
