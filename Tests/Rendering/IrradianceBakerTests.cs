using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class IrradianceBakerTests : IDisposable
{
    private readonly IrradianceBakerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void CapturesLightBouncedOffLitSurfaces()
    {
        IrradianceProbe probe = _builder.WithSunlitRedFloor().BakeOneProbeAboveTheFloor(bounces: 1);

        Vector3 facingDown = probe.Evaluate(-Vector3.UnitY);
        Vector3 facingUp = probe.Evaluate(Vector3.UnitY);

        facingDown.X.Should().BeGreaterThan(0.3f, "the sunlit red floor fills most of the view below");
        facingDown.Y.Should().BeLessThan(0.01f, "the floor reflects no green");
        facingUp.X.Should().BeLessThan(facingDown.X / 4, "nothing above reflects light");
    }

    [Fact]
    public void AddsTheProbesOfEachBounceToTheNext()
    {
        IrradianceProbe first = _builder.WithSunlitRedFloor().WithRedCeiling().BakeOneProbeAboveTheFloor(bounces: 1);
        IrradianceProbe second = _builder.BakeOneProbeAboveTheFloor(bounces: 2);

        Vector3 firstUp = first.Evaluate(Vector3.UnitY);
        Vector3 secondUp = second.Evaluate(Vector3.UnitY);

        firstUp.X.Should().BeLessThan(0.1f, "on the first bounce the ceiling's underside is dark");
        secondUp.X.Should().BeGreaterThan(0.5f, "on the second the probes light the ceiling with what the floor reflects");
    }
}
