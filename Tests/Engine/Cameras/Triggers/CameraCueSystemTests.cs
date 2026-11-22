using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Tests.Triggers;

public sealed class CameraCueSystemTests : IDisposable
{
    private readonly CameraCueSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void EnteringTheAreaCutsToItsShot()
    {
        _builder.Build("""{ "shot": "Window" }""");

        _builder.WalkInto();

        _builder.Game.Director.Live.Should().Be(_builder.Window);
    }

    [Fact]
    public void ACueCanPushTheShotAndReturnOnLeaving()
    {
        _builder.Build("""{ "shot": "Window", "mode": "Push" }""");
        _builder.Game.Spawn("""
            { "name": "LeaveArea", "components": { "transform": { "position": [5, 0, 0] }, "triggerArea": { "size": [2, 2, 2] },
              "cameraCue": { "on": "Exit", "mode": "Return" } } }
            """);

        _builder.WalkInto();
        bool pushed = _builder.Game.Director.Live == _builder.Window;
        _builder.WalkOut();

        pushed.Should().BeTrue();
        _builder.Game.Director.Live.Should().Be(_builder.Room);
    }

    [Fact]
    public void ACueCanPlayItsShotWithATransition()
    {
        _builder.Build("""{ "shot": "Window", "play": true, "transition": { "kind": "Blend", "duration": 0.5 } }""");

        _builder.WalkInto();
        _builder.Game.Run(2);

        _builder.Game.Director.Live.Should().Be(_builder.Window);
        _builder.Game.Director.GetTime(_builder.Window).Should().Be(1);
        _builder.Game.Extract().Cameras[0].Transform.Position.Z.Should().BeApproximately(4, 1e-3f);
    }

    [Fact]
    public void ACueFiresOnlyWhileItsFactsHold()
    {
        _builder.Build("""{ "shot": "Window", "when": [ { "fact": "Fact.Window", "value": 1 } ] }""");

        _builder.WalkInto();
        bool before = _builder.Game.Director.Live == _builder.Window;
        _builder.WalkOut();
        _builder.Game.Game.Facts.SetFact(new Tag("Fact.Window"), 1);
        _builder.WalkInto();

        before.Should().BeFalse();
        _builder.Game.Director.Live.Should().Be(_builder.Window);
    }

    [Fact]
    public void ACueThatFiresOnceFiresOnce()
    {
        _builder.Build("""{ "shot": "Window", "once": true }""");
        _builder.WalkInto();
        _builder.Game.Director.Switch(_builder.Room, ShotTransition.Cut);
        _builder.WalkOut();

        _builder.WalkInto();

        _builder.Game.Director.Live.Should().Be(_builder.Room);
    }

    [Fact]
    public void ACueCanShakeTheCamera()
    {
        _builder.Build("""{ "shakeStrength": 1, "shakeDuration": 1 }""");

        _builder.WalkInto();

        _builder.Game.Director.Live.Should().Be(_builder.Room);
        _builder.Game.Extract().Cameras[0].Transform.Position.Should().NotBe(System.Numerics.Vector3.Zero);
    }
}
