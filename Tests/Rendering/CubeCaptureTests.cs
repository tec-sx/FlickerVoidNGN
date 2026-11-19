using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class CubeCaptureTests
{
    private readonly CubeCaptureTestsBuilder _builder = new();

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, -1, 0, 0)]
    [InlineData(2, 0, 1, 0)]
    [InlineData(3, 0, -1, 0)]
    [InlineData(4, 0, 0, 1)]
    [InlineData(5, 0, 0, -1)]
    public void LooksDownEachAxisInTurn(int face, float x, float y, float z)
    {
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, CubeCapture.GetFaceRotation(face));

        forward.X.Should().BeApproximately(x, 1e-5f);
        forward.Y.Should().BeApproximately(y, 1e-5f);
        forward.Z.Should().BeApproximately(z, 1e-5f);
    }

    [Fact]
    public void MapsTheTopLeftTexelUpAndLeftOfTheFaceAxis()
    {
        Vector3 direction = CubeCapture.GetDirection(face: 5, x: 0, y: 0, CubeCaptureTestsBuilder.Size);

        direction.X.Should().BeNegative();
        direction.Y.Should().BePositive();
        direction.Z.Should().BeNegative();
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0.6f, 0.8f)]
    public void TurnsEvenLightIntoTheSameIrradianceForEveryNormal(float x, float y, float z)
    {
        IrradianceProbe probe = _builder.WithRadiance(static _ => new Vector3(0.5f, 0.25f, 1)).Project();

        Vector3 irradiance = probe.Evaluate(new Vector3(x, y, z));

        irradiance.X.Should().BeApproximately(0.5f, 0.01f);
        irradiance.Y.Should().BeApproximately(0.25f, 0.01f);
        irradiance.Z.Should().BeApproximately(1, 0.01f);
    }

    [Fact]
    public void LightsNormalsFacingTheBrightHalf()
    {
        IrradianceProbe probe = _builder.WithRadiance(static direction => direction.Y > 0 ? Vector3.One : Vector3.Zero).Project();

        Vector3 up = probe.Evaluate(Vector3.UnitY);
        Vector3 down = probe.Evaluate(-Vector3.UnitY);

        up.X.Should().BeApproximately(1, 0.03f, "a surface facing a uniformly bright half of the sky gets all its light");
        down.X.Should().BeApproximately(0, 0.03f);
    }
}
