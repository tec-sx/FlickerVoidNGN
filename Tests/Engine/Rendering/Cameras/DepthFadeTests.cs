using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.Tests.Cameras;

public sealed class DepthFadeTests : IDisposable
{
    private readonly DepthFadeTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void FadesNothingWithoutAMainCamera()
    {
        Entity camera = _builder.WithCamera();

        DepthFade fade = _builder.FindFor(camera);

        fade.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void GivesEveryCameraTheMainCamerasFade()
    {
        MainCamera main = new() { NearAmount = 0.4f, FarAmount = 0.6f, FarColor = Vector3.UnitX };
        _builder.WithMainCamera(main);
        Entity other = _builder.WithCamera();

        DepthFade fade = _builder.FindFor(other);

        fade.Should().Be(main.ToDepthFade());
    }

    [Fact]
    public void LetsACameraOverrideOnlyTheSidesItNames()
    {
        MainCamera main = new() { NearAmount = 0.4f, FarAmount = 0.6f, FarColor = Vector3.UnitX };
        _builder.WithMainCamera(main);
        DepthFadeOverride change = new() { OverrideNear = false, FarStart = 7, FarEnd = 9, FarAmount = 1, FarColor = Vector3.UnitZ };
        Entity shot = _builder.WithCamera(change);

        DepthFade fade = _builder.FindFor(shot);

        fade.NearAmount.Should().Be(0.4f, "the near side stays the main camera's");
        fade.FarStart.Should().Be(7);
        fade.FarEnd.Should().Be(9);
        fade.FarAmount.Should().Be(1);
        fade.FarColor.Should().Be(Vector3.UnitZ);
    }

    [Fact]
    public void BlendsEveryValueBetweenTwoFades()
    {
        DepthFade from = new(0, 2, 0, 1, 3, 0, Vector3.Zero);
        DepthFade to = new(2, 4, 1, 3, 5, 1, Vector3.One);

        DepthFade halfway = DepthFade.Lerp(from, to, 0.5f);

        halfway.Should().Be(new DepthFade(1, 3, 0.5f, 2, 4, 0.5f, new Vector3(0.5f)));
    }
}
