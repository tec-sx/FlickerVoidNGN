using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Cameras.Tests.Directing;

public sealed class CameraDirectorTests : IDisposable
{
    private const float Precision = 1e-3f;
    private const string Line = """{ "position": [0, 0, 0], "time": 0 }, { "position": [10, 0, 0], "time": 2 }, { "position": [20, 0, 0], "time": 4 }""";

    private readonly CameraDirectorTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void StartsOnTheShotMarkedToStart()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""");
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""", start: true);

        _builder.Run(1 / 60f);

        director.Live.Should().Be(cellar);
        _builder.Drawn().Transform.Position.Should().Be(new Vector3(2, 0, 0));
    }

    [Fact]
    public void StartsOnTheFirstShotWhenNoneIsMarked()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""");
        _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""");

        _builder.Run(1 / 60f);

        director.Live.Should().Be(hall);
    }

    [Fact]
    public void AShotWithoutKeysIsStillAtItsOwnTransform()
    {
        _builder.Build();
        _builder.Spawn("""{ "name": "Still", "components": { "transform": { "position": [3, 4, 5] }, "camera": {}, "shot": {} } }""");

        _builder.Run(1 / 60f);

        _builder.Drawn().Transform.Position.Should().Be(new Vector3(3, 4, 5));
    }

    [Fact]
    public void HoldsAtTheFirstKeyUntilPlayed()
    {
        _builder.Build();
        _builder.SpawnShot("Hall", Line);

        _builder.Run(1);

        _builder.Drawn().Transform.Position.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void PlaysToAKeyAtItsTimeAndHoldsThere()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line);
        List<ShotFinishedEvent> finished = _builder.Collect<ShotFinishedEvent>();
        _builder.Run(1 / 60f);

        director.Play(hall, 1);
        _builder.Run(1);
        float halfway = _builder.Drawn().Transform.Position.X;
        _builder.Run(2);

        halfway.Should().BeApproximately(5, 0.1f);
        _builder.Drawn().Transform.Position.X.Should().BeApproximately(10, Precision);
        director.GetTime(hall).Should().Be(2);
        finished.Should().ContainSingle().Which.Shot.Should().Be(hall);
    }

    [Fact]
    public void PlaysToTheEndWithoutAKey()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line);
        _builder.Run(1 / 60f);

        director.Play(hall);
        _builder.Run(5);

        _builder.Drawn().Transform.Position.X.Should().BeApproximately(20, Precision);
    }

    [Fact]
    public void PlaysBackToAnEarlierKey()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line);
        _builder.Run(1 / 60f);
        director.Play(hall);
        _builder.Run(5);

        director.Play(hall, 1);
        _builder.Run(1);

        _builder.Drawn().Transform.Position.X.Should().BeApproximately(15, 0.1f);
    }

    [Fact]
    public void EasesIntoAKey()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [0, 0, 0], "time": 0 }, { "position": [10, 0, 0], "time": 2, "ease": "EaseIn" }""");
        _builder.Run(1 / 60f);

        director.Play(hall);
        _builder.Run(1);

        _builder.Drawn().Transform.Position.X.Should().BeApproximately(2.5f, 0.1f, "easing in covers a quarter of the way in half the time");
    }

    [Fact]
    public void TurnsAndZoomsBetweenKeys()
    {
        CameraDirector director = _builder.Build();
        Quaternion quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        string keys = $$"""
            { "time": 0, "zoom": 4 },
            { "time": 2, "zoom": 8, "rotation": [{{quarter.X}}, {{quarter.Y}}, {{quarter.Z}}, {{quarter.W}}] }
            """;
        Entity hall = _builder.SpawnShot("Hall", keys);
        _builder.Run(1 / 60f);

        director.Play(hall);
        _builder.Run(1);

        RenderCamera drawn = _builder.Drawn();
        drawn.Camera.OrthographicHeight.Should().BeApproximately(6, 0.1f);
        Quaternion eighth = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
        MathF.Abs(Quaternion.Dot(drawn.Transform.Rotation, eighth)).Should().BeGreaterThan(0.999f);
    }

    [Fact]
    public void ZoomsAPerspectiveCameraByItsFieldOfView()
    {
        _builder.Build();
        _builder.Spawn("""
            { "name": "Wide", "components": { "transform": {}, "camera": { "projection": "Perspective", "fieldOfView": 60 },
              "shot": { "keys": [ { "zoom": 35 } ] } } }
            """);

        _builder.Run(1 / 60f);

        RenderCamera drawn = _builder.Drawn();
        drawn.Camera.FieldOfView.Should().Be(35);
        drawn.Camera.OrthographicHeight.Should().Be(10, "an orthographic height is left as the camera has it");
    }

    [Fact]
    public void AZeroZoomKeepsTheCamerasOwn()
    {
        _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""");

        _builder.Run(1 / 60f);

        _builder.Drawn().Camera.OrthographicHeight.Should().Be(10);
    }

    [Fact]
    public void PlacesKeysInTheShotEntitysSpaceAndLeavesItsTransformAlone()
    {
        _builder.Build();
        Quaternion quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Entity turned = _builder.Spawn($$"""
            { "name": "Turned", "components": {
              "transform": { "position": [1, 0, 0], "rotation": [{{quarter.X}}, {{quarter.Y}}, {{quarter.Z}}, {{quarter.W}}] },
              "camera": {}, "shot": { "keys": [ { "position": [0, 0, -2] } ] } } }
            """);

        _builder.Run(1 / 60f);

        RenderCamera drawn = _builder.Drawn();
        Vector3.Distance(drawn.Transform.Position, new Vector3(-1, 0, 0)).Should().BeLessThan(Precision);
        MathF.Abs(Quaternion.Dot(drawn.Transform.Rotation, quarter)).Should().BeGreaterThan(0.999f);
        _builder.Game.World.Get<Transform>(turned).Position.Should().Be(new Vector3(1, 0, 0));
    }

    [Fact]
    public void ACutMakesAnotherShotDrawn()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""");
        List<ShotChangedEvent> changes = _builder.Collect<ShotChangedEvent>();
        _builder.Run(1 / 60f);

        director.Switch(cellar, ShotTransition.Cut);
        _builder.Run(1 / 60f);

        director.Live.Should().Be(cellar);
        _builder.Drawn().Transform.Position.Should().Be(new Vector3(2, 0, 0));
        _builder.Game.World.Has<ActiveCamera>(hall).Should().BeFalse();
        changes.Should().HaveCount(2);
        changes[1].Should().Be(new ShotChangedEvent(hall, cellar));
    }

    [Fact]
    public void ReturnGoesBackToTheShotLiveBeforeAPush()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cutscene = _builder.SpawnShot("Cutscene", """{ "position": [2, 0, 0] }""");
        _builder.Run(1 / 60f);

        director.Push(cutscene, ShotTransition.Cut);
        _builder.Run(1 / 60f);
        Entity during = director.Live;
        director.Return(ShotTransition.Cut);
        _builder.Run(1 / 60f);

        during.Should().Be(cutscene);
        director.Live.Should().Be(hall);
        director.Stack.Should().BeEmpty();
    }

    [Fact]
    public void ReturnWithNothingPushedKeepsTheLiveShot()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""");
        _builder.Run(1 / 60f);

        director.Return(ShotTransition.Cut);
        _builder.Run(1 / 60f);

        director.Live.Should().Be(hall);
    }

    [Fact]
    public void FindsAnotherShotWhenTheLiveOneIsDestroyed()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""");
        _builder.Run(1 / 60f);

        _builder.Game.Lifecycle.Destroy(_builder.Game.World, hall);
        _builder.Run(1 / 60f);

        director.Live.Should().Be(cellar);
    }
}
