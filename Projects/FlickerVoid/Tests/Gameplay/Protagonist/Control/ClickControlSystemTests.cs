using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

public sealed class ClickControlSystemTests : IDisposable
{
    private const string Hero = """
        { "name": "Hero", "components": { "transform": { "position": [4, 0, 0] }, "protagonist": {}, "characterMover": {}, "interactor": { "reach": 1.5 } } }
        """;

    private const string LockedDoor = """
        {
          "type": "interactable",
          "offers": [
            { "input": "Input.Use", "action": "Interaction.Action.Unlock", "weight": 1, "display": { "name": "Unlock" },
              "conditions": { "conditions": [ { "$type": "locked" } ] } },
            { "input": "Input.Use", "action": "Interaction.Action.Toggle", "display": { "name": "Open" } }
          ]
        }
        """;

    private const string Door = """
        { "name": "Door", "components": { "transform": {},
          "staticBody": { "collider": { "type": "box", "size": [1, 2, 0.1], "offset": [0.5, 1, 0] } },
          "interactable": { "definition": "Door.fvdata" },
          "lock": { "action": "Interaction.Action.Unlock" },
          "toggle": { "action": "Interaction.Action.Toggle" },
          "doorSwing": {} } }
        """;

    private readonly ClickControlSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AClickOnTheGroundWalksTheProtagonistThere()
    {
        _builder.Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn(string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero));
        Vector3 spot = new(1, 0, 2);

        _builder.ClickAt(spawned[0], spot);
        _builder.Run(3);

        Vector3 position = _builder.Game.World.Get<Transform>(spawned[2]).Position;
        Vector3.Distance(position, spot).Should().BeLessThan(0.1f);
    }

    [Fact]
    public void ClicksOnALockedDoorWalkToItUnlockItAndSwingItOpen()
    {
        _builder.WithFile("Door.fvdata", LockedDoor).Build();
        IReadOnlyList<Entity> spawned = _builder.Spawn(string.Join(", ", ClickControlSystemTestsBuilder.Camera, ClickControlSystemTestsBuilder.Floor, Hero, Door));
        Entity camera = spawned[0];
        Entity hero = spawned[2];
        Entity door = spawned[3];
        Vector3 panel = new(0.5f, 0, 0);

        _builder.ClickAt(camera, panel);
        _builder.Run(2.5f);
        bool unlocked = !_builder.Game.World.Get<Lockable>(door).Locked;
        _builder.ClickAt(camera, panel);
        _builder.Run(1);

        Vector3 position = _builder.Game.World.Get<Transform>(hero).Position;
        position.X.Should().BeApproximately(1.125f, 0.06f);
        unlocked.Should().BeTrue();
        _builder.Game.World.Get<Toggle>(door).Open.Should().BeTrue();
        Quaternion rotation = _builder.Game.World.Get<Transform>(door).Rotation;
        Vector3 across = Vector3.Transform(Vector3.UnitX, rotation);
        across.Z.Should().BeApproximately(-1, 1e-3f);
    }
}
