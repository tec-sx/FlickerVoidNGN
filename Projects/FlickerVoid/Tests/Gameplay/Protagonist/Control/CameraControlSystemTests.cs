using System.Numerics;

using Arch.Core;

using Silk.NET.Input;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

public sealed class CameraControlSystemTests : IDisposable
{
    private readonly CameraControlSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void GivesTheDirectorThePointerFromTheCentreOfTheWindow()
    {
        _builder.Build();

        _builder.PutMouse(150, 25);
        _builder.Run(1 / 60f);

        _builder.Director.Pointer.Should().Be(new Vector2(0.5f, 0.5f));
    }

    [Fact]
    public void SkipSkipsASkippableCutscene()
    {
        _builder.Build();
        Entity cutscene = _builder.Spawn("""
            { "name": "Cutscene", "components": { "transform": {}, "camera": {},
              "shot": { "skippable": true, "keys": [ { "time": 0 }, { "position": [5, 0, 0], "time": 4 } ] } } }
            """);
        _builder.Run(1 / 60f);
        _builder.Director.Play(cutscene);

        _builder.Hold(Key.Space);
        _builder.Run(2 / 60f);

        _builder.Director.GetTime(cutscene).Should().Be(4);
    }
}
