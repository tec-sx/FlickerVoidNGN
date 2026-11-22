using System.Numerics;

using Arch.Core;

namespace FlickerVoid.Engine.Cameras.Tests.Triggers;

/// <summary>
/// Builds a playing game with a starting Room shot, a Window shot and a fox that sets off trigger areas, and a trigger area two
/// metres wide around x = 5 with the camera cue given.
/// </summary>
internal sealed class CameraCueSystemTestsBuilder : IDisposable
{
    public CameraTestGame Game { get; } = new();

    public Entity Room { get; private set; }

    public Entity Window { get; private set; }

    public Entity Fox { get; private set; }

    public CameraCueSystemTestsBuilder Build(string cue)
    {
        Room = Game.SpawnShot("Room", 0, extra: """, "start": true""");
        Window = Game.SpawnShot("Window", 10, """{ "position": [0, 0, 0], "time": 0 }, { "position": [0, 0, 4], "time": 1 }""");
        Fox = Game.Spawn("""{ "name": "Fox", "components": { "transform": {}, "triggerActivator": {} } }""");
        Game.Spawn($$"""
            { "name": "WindowArea", "components": { "transform": { "position": [5, 0, 0] }, "triggerArea": { "size": [2, 2, 2] },
              "cameraCue": {{cue}} } }
            """);
        Game.Run(1 / 60f);
        return this;
    }

    public void WalkInto() => Walk(new Vector3(5, 0, 0));

    public void WalkOut() => Walk(Vector3.Zero);

    public void Dispose() => Game.Dispose();

    private void Walk(Vector3 position)
    {
        Game.MoveTo(Fox, position);
        Game.Run(2 / 60f);
    }
}
