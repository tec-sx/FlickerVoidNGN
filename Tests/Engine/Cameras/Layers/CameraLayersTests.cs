using System.Numerics;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Rendering.Extraction;

namespace FlickerVoid.Engine.Cameras.Tests.Layers;

public sealed class CameraLayersTests : IDisposable
{
    private const float Precision = 1e-3f;

    private readonly CameraLayersTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AShotWithoutARigStaysOnItsRail()
    {
        _builder.Build(new Vector3(30, 0, -5));

        _builder.Run(2);

        _builder.Drawn().Transform.Position.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void FollowLeansTowardsTheTargetWithinItsRadius()
    {
        _builder.WithRig("""{ "$type": "follow", "strength": 0.5, "radius": 1, "damping": 0.2 }""");
        _builder.Build(new Vector3(1, 0, -5));
        _builder.Run(2);
        Vector3 near = _builder.Drawn().Transform.Position;

        _builder.MoveTarget(new Vector3(40, 30, -5));
        float furthest = 0;
        for (int frame = 0; frame < 120; frame++)
        {
            _builder.Run(1 / 60f);
            float distance = _builder.Drawn().Transform.Position.Length();
            furthest = MathF.Max(furthest, distance);
        }

        near.X.Should().BeApproximately(0.5f, 0.01f, "it leans half the target's offset from the centre");
        near.Z.Should().Be(0, "it slides across the view, never into it");
        furthest.Should().BeLessThanOrEqualTo(1 + Precision);
        furthest.Should().BeGreaterThan(0.99f);
    }

    [Fact]
    public void LookDriftsTowardsThePointerAndSettlesBack()
    {
        _builder.WithRig("""{ "$type": "look", "radius": 0.5, "deadzone": 0.2, "damping": 0.2, "returnTime": 0.2 }""");
        CameraDirector director = _builder.Build(Vector3.Zero);

        director.Pointer = new Vector2(1, 1);
        _builder.Run(2);
        Vector3 drifted = _builder.Drawn().Transform.Position;
        director.Pointer = new Vector2(0.1f, 0);
        _builder.Run(2);

        drifted.Length().Should().BeApproximately(0.5f, 0.01f, "the drift stops at the radius");
        drifted.X.Should().BePositive();
        drifted.Y.Should().BePositive("the pointer is above the centre");
        _builder.Drawn().Transform.Position.Length().Should().BeLessThan(0.01f, "within the deadzone it settles back");
    }

    [Fact]
    public void AShakeStaysWithinItsAmplitudeAndFadesToNothing()
    {
        _builder.WithRig("""{ "$type": "shake", "positionAmplitude": 0.2, "rotationAmplitude": 2, "frequency": 10 }""");
        _builder.Build(Vector3.Zero);
        _builder.Run(1 / 60f);

        _builder.Game.Events.Send(new CameraShakeCommand(1, 1));
        float furthest = 0;
        for (int frame = 0; frame < 60; frame++)
        {
            _builder.Run(1 / 60f);
            float distance = _builder.Drawn().Transform.Position.Length();
            furthest = MathF.Max(furthest, distance);
        }

        _builder.Run(1 / 60f);

        furthest.Should().BeGreaterThan(0.01f);
        furthest.Should().BeLessThanOrEqualTo(MathF.Sqrt(2) * 0.2f + Precision);
        RenderCamera settled = _builder.Drawn();
        settled.Transform.Position.Should().Be(Vector3.Zero);
        MathF.Abs(settled.Transform.Rotation.W).Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void AShotWithoutAShakeLayerStillShakes()
    {
        _builder.Build(Vector3.Zero);
        _builder.Run(1 / 60f);

        _builder.Game.Events.Send(new CameraShakeCommand(1, 1));
        _builder.Run(0.2f);

        _builder.Drawn().Transform.Position.Should().NotBe(Vector3.Zero);
    }

    [Fact]
    public void AZoomPunchZoomsInAndBack()
    {
        _builder.WithRig("""{ "$type": "zoomPunch", "amount": -0.2, "attack": 0.1, "release": 0.4 }""");
        _builder.Build(Vector3.Zero);
        _builder.Run(1 / 60f);

        _builder.Game.Events.Send(new ZoomPunchCommand(1));
        _builder.Run(0.1f);
        float punched = _builder.Drawn().Camera.OrthographicHeight;
        _builder.Run(1);

        punched.Should().BeApproximately(8, 0.1f);
        _builder.Drawn().Camera.OrthographicHeight.Should().Be(10);
    }

    [Fact]
    public void ACutsceneCanPlayWithoutLayers()
    {
        _builder.WithRig("""{ "$type": "follow", "strength": 1, "radius": 1, "damping": 0 }""");
        CameraDirector director = _builder.Build(new Vector3(0.5f, 0, -5), """{ "position": [0, 0, 0], "time": 0 }, { "position": [0, 0, 0], "time": 1 }""");
        _builder.Run(1 / 60f);

        director.Play(_builder.Shot, layers: false);
        _builder.Run(0.5f);
        float during = _builder.Drawn().Transform.Position.X;
        _builder.Run(1);

        during.Should().Be(0);
        _builder.Drawn().Transform.Position.X.Should().BeApproximately(0.5f, Precision);
    }
}
