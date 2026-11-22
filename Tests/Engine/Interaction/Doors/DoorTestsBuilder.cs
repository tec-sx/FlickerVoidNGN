using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Interaction.Tests.Interactors;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Engine.Interaction.Tests.Doors;

/// <summary>
/// Builds a playing game with saves in a temporary folder and a hero beside a saveable door that opens with Use and unlocks with Unlock
/// while locked, the door's components given as scene JSON.
/// </summary>
internal sealed class DoorTestsBuilder : IDisposable
{
    private const string Definition = """
        {
          "type": "interactable",
          "offers": [
            { "input": "Input.Use", "action": "Interaction.Action.Toggle" },
            { "input": "Input.Unlock", "action": "Interaction.Action.Unlock",
              "conditions": { "conditions": [ { "$type": "locked" } ] } }
          ]
        }
        """;

    private readonly InteractionSystemTestsBuilder _game = new();
    private readonly string _saves = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string _door = """
        "toggle": { "action": "Interaction.Action.Toggle" },
        "lock": { "action": "Interaction.Action.Unlock" }
        """;

    public Entity Hero { get; private set; }

    public Entity Door { get; private set; }

    public World World => _game.Game.World;

    public SaveSystem Saves { get; private set; } = null!;

    public DoorTestsBuilder WithDoor(string components)
    {
        _door = components;
        return this;
    }

    public DoorTestsBuilder Build()
    {
        _game.WithFile("Door.fvdata", Definition).Build();
        Saves = _game.Game.AddSaves(new SaveSettings { Folder = _saves });
        IReadOnlyList<Entity> spawned = _game.Spawn($$"""
            { "name": "Hero", "components": { "transform": {}, "interactor": {} } },
            { "name": "Door", "components": { "transform": { "position": [1, 0, 0] }, "interactable": { "definition": "Door.fvdata" },
              "saveable": { "id": "Door" }, {{_door}} } }
            """);
        Hero = spawned[0];
        Door = spawned[1];
        _game.Send(new FocusCommand(Hero, Door));
        return this;
    }

    public List<T> Collect<T>()
        where T : struct => _game.Collect<T>();

    /// <summary>Presses an input of the hero's at the door.</summary>
    public void Press(string input)
    {
        Tag tag = new(input);
        _game.Send(new InteractionInput(Hero, tag, InputPhase.Pressed));
    }

    public void Dispose()
    {
        _game.Dispose();
        if (Directory.Exists(_saves))
        {
            Directory.Delete(_saves, recursive: true);
        }
    }
}
