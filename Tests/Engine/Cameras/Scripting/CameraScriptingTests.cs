using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Scripting;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Tests.Scripting;

public sealed class CameraScriptingTests : IDisposable
{
    private readonly CameraScriptingTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheCutToShotEffectSwitchesShots()
    {
        _builder.Apply(new CutToShotEffect { Shot = "Cellar" });
        _builder.Game.Run(1 / 60f);

        _builder.Game.Director.Live.Should().Be(_builder.Cellar);
    }

    [Fact]
    public void TheCutToShotEffectCanFade()
    {
        ShotTransition fade = new() { Kind = TransitionKind.Fade, Duration = 0.5f, InDuration = 0.5f };

        _builder.Apply(new CutToShotEffect { Shot = "Cellar", Transition = fade });
        _builder.Game.Run(0.25f);

        _builder.Game.Director.Live.Should().Be(_builder.Hall);
        _builder.Game.Extract().Transition.Fade.Should().BeApproximately(0.5f, 0.05f);
    }

    [Fact]
    public void ThePlayShotEffectPlaysAShotToAKey()
    {
        _builder.Game.Director.Switch(_builder.Cellar, ShotTransition.Cut);

        _builder.Apply(new PlayShotEffect { Shot = "Cellar", ToKey = 1 });
        _builder.Game.Run(2);

        _builder.Game.Director.GetTime(_builder.Cellar).Should().Be(1);
    }

    [Fact]
    public void TheShakeCameraEffectShakesTheLiveCamera()
    {
        _builder.Apply(new ShakeCameraEffect { Strength = 1, Duration = 1 });
        _builder.Game.Run(0.2f);

        _builder.Game.Extract().Cameras[0].Transform.Position.Should().NotBe(Vector3.Zero);
    }

    [Fact]
    public void ACutsceneSwitchesPlaysAndReturns()
    {
        FlowRun run = _builder.Run(
            new SwitchShotNode { Shot = "Cellar", Mode = ShotSwitchMode.Push },
            new PlayShotNode { Shot = "Cellar", Layers = false },
            new SwitchShotNode { Mode = ShotSwitchMode.Return });
        _builder.Game.Run(1 / 60f);
        Entity during = _builder.Game.Director.Live;
        bool runningDuring = run.IsRunning;

        _builder.Game.Run(2.5f);

        during.Should().Be(_builder.Cellar);
        runningDuring.Should().BeTrue("the play node waits for the shot to finish");
        _builder.Game.Director.Live.Should().Be(_builder.Hall);
        run.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ASwitchWaitsForItsTransitionToFinish()
    {
        ShotTransition fade = new() { Kind = TransitionKind.Fade, Duration = 0.5f, InDuration = 0.5f };
        FlowRun run = _builder.Run(new SwitchShotNode { Shot = "Cellar", Transition = fade });

        _builder.Game.Run(0.75f);
        bool runningDuring = run.IsRunning;
        _builder.Game.Run(0.5f);

        runningDuring.Should().BeTrue();
        run.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void WaitForShotMarkerHoldsTheSignalUntilTheMarker()
    {
        _builder.Game.Director.Switch(_builder.Cellar, ShotTransition.Cut);
        _builder.Game.Director.Play(_builder.Cellar);
        FlowRun run = _builder.Run(new WaitForShotMarkerNode { Shot = "Cellar", Marker = new Tag("Marker.Door") });

        _builder.Game.Run(0.5f);
        bool runningBefore = run.IsRunning;
        _builder.Game.Run(0.6f);

        runningBefore.Should().BeTrue();
        run.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void TheFadeNodeFadesOutAndIn()
    {
        FlowRun run = _builder.Run(
            new FadeNode { Direction = FadeDirection.Out, Duration = 0.5f, Color = Vector3.UnitX },
            new FadeNode { Direction = FadeDirection.In, Duration = 0.5f });

        _builder.Game.Run(0.6f);
        float dark = _builder.Game.Extract().Transition.Fade;
        _builder.Game.Run(0.6f);

        dark.Should().BeGreaterThan(0.75f);
        _builder.Game.Extract().Transition.Fade.Should().Be(0);
        run.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void TheShakeNodeShakesAndGoesOn()
    {
        FlowRun run = _builder.Run(new ShakeCameraNode { Strength = 1, Duration = 1 });

        _builder.Game.Run(0.2f);

        run.IsRunning.Should().BeFalse();
        _builder.Game.Extract().Cameras[0].Transform.Position.Should().NotBe(Vector3.Zero);
    }

    [Fact]
    public void AResumedSwitchOrFadeGoesOnWithoutSwitchingOrFadingAgain()
    {
        FlowRun switched = _builder.Resume("node0", new SwitchShotNode { Shot = "Cellar" });
        FlowRun faded = _builder.Resume("node0", new FadeNode { Direction = FadeDirection.Out, Duration = 0.5f });

        _builder.Game.Run(0.25f);

        switched.IsRunning.Should().BeFalse();
        faded.IsRunning.Should().BeFalse();
        _builder.Game.Director.Live.Should().Be(_builder.Hall, "the save holds the shot the switch went to");
        _builder.Game.Extract().Transition.Fade.Should().Be(0, "the save holds the fade");
    }

    [Fact]
    public void AResumedPlayPlaysOnToItsKey()
    {
        _builder.Game.Director.Switch(_builder.Cellar, ShotTransition.Cut);

        FlowRun run = _builder.Resume("node0", new PlayShotNode { Shot = "Cellar", ToKey = 1 });
        _builder.Game.Run(2);

        run.IsRunning.Should().BeFalse();
        _builder.Game.Director.GetTime(_builder.Cellar).Should().Be(1);
    }

    [Fact]
    public void AFinishedRunStopsWaiting()
    {
        FlowRun run = _builder.Run(new PlayShotNode { Shot = "Cellar" });

        run.Finish();
        _builder.Game.Run(3);

        run.IsRunning.Should().BeFalse();
    }
}
