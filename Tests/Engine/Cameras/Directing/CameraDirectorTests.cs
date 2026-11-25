using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Shots;
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
    public void HoldsAPlayingShotWhileTheGameIsPaused()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line);
        _builder.Run(1 / 60f);
        director.Play(hall, 1);
        _builder.Run(1);

        _builder.Game.Pause(new Gameplay.Tags.Tag("Pause.Hub"));
        _builder.Run(1);
        float paused = director.GetTime(hall);
        _builder.Game.Resume(new Gameplay.Tags.Tag("Pause.Hub"));
        _builder.Run(2);

        paused.Should().BeApproximately(1, 0.02f);
        director.GetTime(hall).Should().Be(2);
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
    public void CountsTheCutsToNewViewsButNotTheBlendsThere()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""");
        Entity attic = _builder.SpawnShot("Attic", """{ "position": [3, 0, 0] }""");
        _builder.Run(1 / 60f);
        uint first = _builder.Extract().Cuts;

        director.Switch(cellar, ShotTransition.Cut);
        _builder.Run(1 / 60f);
        uint afterCut = _builder.Extract().Cuts;
        director.Switch(attic, new ShotTransition { Kind = TransitionKind.Blend, Duration = 1 });
        _builder.Run(1 / 60f);
        uint afterBlend = _builder.Extract().Cuts;
        director.Switch(hall, new ShotTransition { Kind = TransitionKind.CrossFade, Duration = 1 });
        _builder.Run(1 / 60f);
        uint afterCrossFade = _builder.Extract().Cuts;

        first.Should().BeGreaterThan(0, "the first shot going live cuts to its view");
        afterCut.Should().Be(first + 1);
        afterBlend.Should().Be(afterCut, "a blend moves the view to the next shot");
        afterCrossFade.Should().Be(afterBlend + 1, "a cross-fade draws the next shot at once under the dissolve");
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

    [Fact]
    public void AFadeDarkensTheWorldCutsAndBrightensIt()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [2, 0, 0] }""");
        _builder.Run(1 / 60f);
        ShotTransition fade = new() { Kind = TransitionKind.Fade, Duration = 1, InDuration = 2, Color = Vector3.UnitX };

        director.Switch(cellar, fade);
        _builder.Run(0.5f);
        RenderTransition halfOut = _builder.Transition();
        float halfOutX = _builder.Drawn().Transform.Position.X;
        _builder.Run(1.5f);
        RenderTransition halfIn = _builder.Transition();
        _builder.Run(1.1f);

        halfOut.Fade.Should().BeApproximately(0.5f, 0.02f);
        halfOut.FadeColor.Should().Be(Vector3.UnitX);
        halfOutX.Should().Be(1, "the old shot stays until the world is dark");
        halfIn.Fade.Should().BeApproximately(0.5f, 0.02f);
        director.Live.Should().Be(cellar);
        _builder.Transition().Fade.Should().Be(0);
    }

    [Fact]
    public void FadeOutHoldsUntilFadeIn()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""");
        _builder.Run(1 / 60f);

        director.FadeOut(1, Vector3.Zero);
        _builder.Run(3);
        float held = _builder.Transition().Fade;
        director.FadeIn(0.5f);
        _builder.Run(1);

        held.Should().Be(1);
        _builder.Transition().Fade.Should().Be(0);
        director.IsFading.Should().BeFalse();
    }

    [Fact]
    public void ACrossFadeDissolvesTheOldShotsLiveViewAway()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line, start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [0, 0, 5] }""");
        _builder.Run(1 / 60f);
        director.Play(hall);
        ShotTransition crossFade = new() { Kind = TransitionKind.CrossFade, Duration = 1 };

        director.Switch(cellar, crossFade);
        _builder.Run(0.5f);
        RenderTransition halfway = _builder.Transition();
        _builder.Run(0.6f);

        director.Live.Should().Be(cellar);
        halfway.Dissolve.Should().BeApproximately(0.5f, 0.02f);
        halfway.DissolveFrom.Transform.Position.X.Should().BeApproximately(2.5f, 0.1f, "the old shot keeps playing under the dissolve");
        _builder.Drawn().Transform.Position.Z.Should().Be(5);
        _builder.Transition().Dissolve.Should().Be(0);
    }

    [Fact]
    public void ABlendMovesFromTheDrawnPoseToTheNextRail()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [0, 0, 0], "zoom": 4 }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [10, 0, 0], "zoom": 8 }""");
        List<ShotChangedEvent> changes = _builder.Collect<ShotChangedEvent>();
        _builder.Run(1 / 60f);
        ShotTransition blend = new() { Kind = TransitionKind.Blend, Duration = 2 };

        director.Switch(cellar, blend);
        _builder.Run(1);
        RenderCamera halfway = _builder.Drawn();
        _builder.Run(1.1f);

        director.Live.Should().Be(cellar);
        changes.Should().HaveCount(2);
        halfway.Transform.Position.X.Should().BeApproximately(5, 0.1f);
        halfway.Camera.OrthographicHeight.Should().BeApproximately(6, 0.1f);
        _builder.Drawn().Transform.Position.X.Should().Be(10);
    }

    [Fact]
    public void MeasuresTheLiveShotsDepthFadeFromItsTarget()
    {
        _builder.Build();
        _builder.SpawnTarget("Fox", new Vector3(1, 0, 2));
        _builder.SpawnShot("Hall", """{ "position": [0, 0, 0] }""", start: true);

        _builder.Run(1 / 60f);
        RenderScene scene = _builder.Extract();

        scene.Focus.Should().Be(new Vector3(1, 0, 2));
    }

    [Fact]
    public void ABlendBlendsTheFocusToo()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnTarget("Fox", new Vector3(0, 0, 0));
        _builder.SpawnTarget("Door", new Vector3(10, 0, 0));
        _builder.SpawnShot("Hall", """{ "position": [0, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [10, 0, 0] }""", extra: """, "target": "Door" """);
        _builder.Run(1 / 60f);
        ShotTransition blend = new() { Kind = TransitionKind.Blend, Duration = 2 };

        director.Switch(cellar, blend);
        _builder.Run(1);
        RenderScene halfway = _builder.Extract();
        _builder.Run(1.1f);
        RenderScene after = _builder.Extract();

        halfway.Focus!.Value.X.Should().BeApproximately(5, 0.1f);
        after.Focus.Should().Be(new Vector3(10, 0, 0));
    }

    [Fact]
    public void ABlendEasesAsItSays()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [0, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [10, 0, 0] }""");
        _builder.Run(1 / 60f);
        ShotTransition blend = new() { Kind = TransitionKind.Blend, Duration = 2, Ease = ShotEase.EaseIn };

        director.Switch(cellar, blend);
        _builder.Run(1);

        _builder.Drawn().Transform.Position.X.Should().BeApproximately(2.5f, 0.1f);
    }

    [Fact]
    public void ACutDuringATransitionEndsIt()
    {
        CameraDirector director = _builder.Build();
        _builder.SpawnShot("Hall", """{ "position": [0, 0, 0] }""", start: true);
        Entity cellar = _builder.SpawnShot("Cellar", """{ "position": [10, 0, 0] }""");
        Entity attic = _builder.SpawnShot("Attic", """{ "position": [20, 0, 0] }""");
        _builder.Run(1 / 60f);
        director.Switch(cellar, new ShotTransition { Kind = TransitionKind.CrossFade, Duration = 2 });
        _builder.Run(0.5f);

        director.Switch(attic, ShotTransition.Cut);
        _builder.Run(1 / 60f);

        _builder.Drawn().Transform.Position.X.Should().Be(20);
        _builder.Transition().IsVisible.Should().BeFalse();
    }

    [Fact]
    public void SkippingASkippableShotJumpsToItsLastKey()
    {
        CameraDirector director = _builder.Build();
        Entity cutscene = _builder.SpawnShot("Cutscene", Line, extra: """, "skippable": true""");
        List<ShotFinishedEvent> finished = _builder.Collect<ShotFinishedEvent>();
        _builder.Run(1 / 60f);
        director.Play(cutscene);
        _builder.Run(0.5f);
        bool couldSkip = director.CanSkip;

        director.Skip();
        _builder.Run(1 / 60f);

        couldSkip.Should().BeTrue();
        director.GetTime(cutscene).Should().Be(4);
        _builder.Drawn().Transform.Position.X.Should().BeApproximately(20, Precision);
        finished.Should().ContainSingle();
        director.CanSkip.Should().BeFalse();
    }

    [Fact]
    public void AShotThatIsNotSkippableCannotBeSkipped()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", Line);
        _builder.Run(1 / 60f);
        director.Play(hall);
        _builder.Run(0.5f);

        director.Skip();
        _builder.Run(1 / 60f);

        director.CanSkip.Should().BeFalse();
        director.GetTime(hall).Should().BeLessThan(1);
    }

    [Fact]
    public void FindsShotsByName()
    {
        CameraDirector director = _builder.Build();
        Entity cellar = _builder.SpawnShot("Cellar", Line);

        director.FindShot("Cellar").Should().Be(cellar);
        director.FindShot("Attic").Should().Be(Entity.Null);
    }

    [Fact]
    public void ALoadedGameReturnsTheSameShotAndTime()
    {
        CameraDirector director = _builder.Build(saves: true);
        Entity hall = _builder.SpawnShot("Hall", Line, start: true);
        Entity cellar = _builder.SpawnShot("Cellar", Line);
        _builder.Run(1 / 60f);
        director.Push(cellar, ShotTransition.Cut);
        director.Play(cellar, 1);
        _builder.Run(1);
        float savedTime = director.GetTime(cellar);
        _builder.Saves.Save("Test").Should().BeTrue();

        director.Return(ShotTransition.Cut);
        director.Play(cellar);
        director.FadeOut(0, Vector3.Zero);
        _builder.Run(2);
        _builder.Saves.Load("Test").Should().BeTrue();
        _builder.Run(1 / 60f);

        director.Live.Should().Be(cellar);
        director.Stack.Should().Equal(hall);
        director.GetTime(cellar).Should().BeApproximately(savedTime + (1 / 60f), 1e-4f, "it plays on towards its key after the load");
        _builder.Transition().Fade.Should().Be(0);
        _builder.Run(2);
        director.GetTime(cellar).Should().Be(2);
    }

    [Fact]
    public void PushingTheLiveShotKeepsItOffTheStack()
    {
        CameraDirector director = _builder.Build();
        Entity hall = _builder.SpawnShot("Hall", """{ "position": [1, 0, 0] }""", start: true);
        Entity cutscene = _builder.SpawnShot("Cutscene", """{ "position": [2, 0, 0] }""");
        _builder.Run(1 / 60f);
        director.Push(cutscene, ShotTransition.Cut);

        director.Push(cutscene, ShotTransition.Cut);
        director.Return(ShotTransition.Cut);

        director.Live.Should().Be(hall);
    }
}
