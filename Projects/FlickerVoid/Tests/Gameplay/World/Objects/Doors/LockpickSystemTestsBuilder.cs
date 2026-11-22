using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Gameplay.Tests.World.Objects.Doors;

/// <summary>
/// Builds a playing game with FlickerVoid's gameplay, no window, and the lockpick's data as the game's content holds it: the
/// Perception attribute, the lockpick item, ability and check, and the protagonist's ability set; with a hero and a locked door.
/// </summary>
internal sealed class LockpickSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public LockpickSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Attributes/Perception.fvdata", """
            { "type": "attribute", "id": "Attribute.Skill.Perception", "display": { "name": "Perception" }, "category": "Attribute.Skill", "max": 10, "integer": true }
            """);
        WithFile("Items/Lockpick.fvdata", """{ "type": "item", "id": "Item.Lockpick", "category": "Item.Tool", "tags": [ "Item.Lockpick" ] }""");
        WithFile("Abilities/Lockpick.fvdata", """
            { "type": "ability", "id": "Ability.Lockpick", "group": "ExclusiveReplace", "ownedTagsWhileActive": [ "Status.Interacting" ],
              "conditions": { "conditions": [ { "$type": "hasItemWithTag", "tag": "Item.Lockpick" } ] } }
            """);
        WithFile("Abilities/Protagonist.fvdata", """{ "type": "abilitySet", "abilities": [ { "ability": "Abilities/Lockpick.fvdata" } ] }""");
        WithFile("Checks/Lockpick.fvdata", """{ "type": "check", "attribute": "Attribute.Skill.Perception", "roll": "D6x2" }""");
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Hero { get; private set; }

    public Entity Door { get; private set; }

    /// <summary>Starts play with a hero of the given Perception, carrying a lockpick or not, and a door locked with the given difficulty.</summary>
    public LockpickSystemTestsBuilder Build(int perception, bool lockpick, float difficulty = 0.5f)
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddFlow();
        FlowGraphs graphs = new(content, Game.Data);
        Game.Services.AddService(graphs);
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        Game.Lifecycle.Play(Game.World);
        string items = lockpick ? """{ "item": "Items/Lockpick.fvdata" }""" : string.Empty;
        string json = $$"""
            { "entities": [
                { "name": "Hero", "components": { "transform": {}, "inventory": { "items": [ {{items}} ] },
                    "attributes": { "values": [ { "attribute": "Attribute.Skill.Perception", "value": {{perception}} } ] },
                    "abilities": { "sets": [ "Abilities/Protagonist.fvdata" ] } } },
                { "name": "Door", "components": { "transform": {}, "lock": { "difficulty": {{difficulty}} }, "toggle": { "action": "Interaction.Action.Toggle" } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Door = spawned[1];
        return this;
    }

    public LockpickSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>The hero tries to open the locked door, as a click on it does, and the events run.</summary>
    public void TryOpen()
    {
        Game.Events.Send(new ToggleBlockedEvent(Door, Hero, Opening: true));
        Game.Events.Dispatch();
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
