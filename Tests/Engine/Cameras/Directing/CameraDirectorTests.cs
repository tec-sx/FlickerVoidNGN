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

    private const string Corridor = """{ "position": [0, 0, 0], "time": 0 }, { "position": [10, 0, 0], "time": 10 }""";
    private const string TrackedFox = """, "drive": "Tracked", "smoothing": 0""";

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

    [Fact]
    public void ATrackedShotFollowsItsTargetAlongTheRail()
    {
        _builder.Build();
        _builder.SpawnShot("Corridor", Corridor, extra: TrackedFox);
        Entity fox = _builder.SpawnTarget("Fox", new Vector3(4, 0, 3));
        _builder.Run(1 / 60f);
        float first = _builder.Drawn().Transform.Position.X;

        _builder.MoveTo(fox, new Vector3(8, 0, -2));
        _builder.Run(1 / 60f);
        float further = _builder.Drawn().Transform.Position.X;
        _builder.MoveTo(fox, new Vector3(2, 0, 0));
        _builder.Run(1 / 60f);

        first.Should().BeApproximately(4, Precision);
        further.Should().BeApproximately(8, Precision);
        _builder.Drawn().Transform.Position.X.Should().BeApproximately(2, Precision);
    }

    [Fact]
    public void ATrackedShotSetsItsPlaceOnTheTimeline()
    {
        CameraDirector director = _builder.Build();
        Entity corridor = _builder.SpawnShot("Corridor", Corridor, extra: TrackedFox);
        _builder.SpawnTarget("Fox", new Vector3(5, 0, 0));

        _builder.Run(1 / 60f);

        director.GetTime(corridor).Should().BeApproximately(5, Precision);
    }

    [Fact]
    public void TracksTheNamedTargetElseTheFirstCameraTarget()
    {
        _builder.Build();
        _builder.SpawnShot("Corridor", Corridor, extra: """, "drive": "Tracked", "smoothing": 0, "target": "Cat" """);
        _builder.SpawnTarget("Fox", new Vector3(3, 0, 0));
        _builder.SpawnTarget("Cat", new Vector3(7, 0, 0));

        _builder.Run(1 / 60f);

        _builder.Drawn().Transform.Position.X.Should().BeApproximately(7, Precision);
    }

    [Fact]
    public void SmoothsTheTrackedPoint()
    {
        _builder.Build();
        _builder.SpawnShot("Corridor", Corridor, extra: """, "drive": "Tracked", "smoothing": 0.5 """);
        Entity fox = _builder.SpawnTarget("Fox", Vector3.Zero);
        _builder.Run(1 / 60f);

        _builder.MoveTo(fox, new Vector3(10, 0, 0));
        _builder.Run(1 / 60f);
        float soon = _builder.Drawn().Transform.Position.X;
        _builder.Run(5);

        soon.Should().BeInRange(0.01f, 1);
        _builder.Drawn().Transform.Position.X.Should().BeApproximately(10, Precision);
    }

    [Fact]
    public void WalkingAlongACorridorFiresItsEndMarkerOnce()
    {
        _builder.Build();
        string keys = """{ "position": [0, 0, 0], "time": 0 }, { "position": [10, 0, 0], "time": 10, "marker": "Marker.CorridorEnd" }""";
        Entity corridor = _builder.SpawnShot("Corridor", keys, extra: TrackedFox);
        Entity fox = _builder.SpawnTarget("Fox", Vector3.Zero);
        List<ShotMarkerEvent> markers = _builder.Collect<ShotMarkerEvent>();

        for (float x = 0; x <= 12; x += 0.5f)
        {
            _builder.MoveTo(fox, new Vector3(x, 0, 0));
            _builder.Run(1 / 60f);
        }

        _builder.Run(1);

        ShotMarkerEvent marker = markers.Should().ContainSingle().Subject;
        marker.Shot.Should().Be(corridor);
        marker.Marker.Name.Should().Be("Marker.CorridorEnd");
        marker.Key.Should().Be(1);
    }

    [Fact]
    public void PlayingFiresTheMarkersOfTheKeysItPasses()
    {
        CameraDirector director = _builder.Build();
        string keys = """
            { "position": [0, 0, 0], "time": 0, "marker": "Marker.Start" },
            { "position": [10, 0, 0], "time": 1, "marker": "Marker.Middle" },
            { "position": [20, 0, 0], "time": 2, "marker": "Marker.End" }
            """;
        Entity hall = _builder.SpawnShot("Hall", keys);
        List<ShotMarkerEvent> markers = _builder.Collect<ShotMarkerEvent>();
        _builder.Run(1 / 60f);

        director.Play(hall);
        _builder.Run(3);

        markers.Select(marker => marker.Marker.Name).Should().Equal("Marker.Middle", "Marker.End");
    }

    [Fact]
    public void LooksAtItsTarget()
    {
        _builder.Build();
        _builder.SpawnShot("Watcher", """{ "position": [0, 0, 0] }""", extra: """, "lookAtTarget": true""");
        Entity fox = _builder.SpawnTarget("Fox", new Vector3(0, 0, -5));
        _builder.Run(1 / 60f);
        Quaternion ahead = _builder.Drawn().Transform.Rotation;

        _builder.MoveTo(fox, new Vector3(5, 0, 0));
        _builder.Run(1 / 60f);
        Quaternion turned = _builder.Drawn().Transform.Rotation;

        MathF.Abs(Quaternion.Dot(ahead, Quaternion.Identity)).Should().BeGreaterThan(0.999f);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, turned);
        Vector3.Distance(forward, Vector3.UnitX).Should().BeLessThan(Precision);
    }

    [Fact]
    public void LooksAtTheTargetsAimHeight()
    {
        _builder.Build();
        _builder.SpawnShot("Watcher", """{ "position": [0, 0, 0] }""", extra: """, "lookAtTarget": true""");
        _builder.Spawn("""{ "name": "Fox", "components": { "transform": { "position": [0, 0, -5] }, "cameraTarget": { "height": 5 } } }""");

        _builder.Run(1 / 60f);

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, _builder.Drawn().Transform.Rotation);
        Vector3 expected = Vector3.Normalize(new Vector3(0, 5, -5));
        Vector3.Distance(forward, expected).Should().BeLessThan(Precision);
    }
}
