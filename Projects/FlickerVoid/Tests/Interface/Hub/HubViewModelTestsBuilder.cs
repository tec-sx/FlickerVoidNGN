using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Story.Knowledge;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay;
using FlickerVoid.Interface.Hub;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Interface.Tests.Hub;

/// <summary>
/// Builds the hub over a playing game with FlickerVoid's gameplay and no window: a protagonist carrying two mugs of ale and a cap,
/// with 12 coins, two attributes, and the cellar quest and a topic to know about.
/// </summary>
internal sealed class HubViewModelTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public HubViewModelTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Items/Ale.fvdata", """
            { "type": "item", "id": "Item.Ale", "display": { "name": "Mug of ale", "shortDescription": "Dark and strong." }, "weight": 0.5,
              "fragments": [ { "$type": "usable", "consumeOnUse": true } ] }
            """);
        WithFile("Items/Cap.fvdata", """
            { "type": "item", "id": "Item.Cap", "display": { "name": "Flat cap" }, "weight": 0.5, "maxQuantity": 1,
              "fragments": [ { "$type": "equippable", "slots": [ "Equipment.Slot.Head" ] } ] }
            """);
        WithFile("Attributes/Stealth.fvdata", """
            { "type": "attribute", "id": "Attribute.Skill.Stealth", "display": { "name": "Stealth" }, "max": 10, "integer": true }
            """);
        WithFile("Quests/Cellar.fvdata", """
            { "type": "quest", "id": "Quest.Cellar", "display": { "name": "The cellar key" },
              "objectives": [ { "fact": "Quest.Cellar.FoundKey", "description": "Find the cellar key", "completeWhen": { "conditions": [ { "$type": "fact", "fact": "Has.Key" } ] } } ] }
            """);
        WithFile("Knowledge/Cellar.fvdata", """
            { "type": "knowledge", "id": "Knowledge.Cellar", "display": { "name": "The cellar", "shortDescription": "Locked since spring." } }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Protagonist { get; private set; }

    public HubViewModel Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddFlow();
        FlowGraphs graphs = new(content, Game.Data);
        Game.Services.AddService(graphs);
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        string json = """
            { "entities": [
                { "name": "Fox", "components": { "transform": {}, "protagonist": {},
                    "inventory": { "items": [ { "item": "Items/Ale.fvdata", "quantity": 2 }, { "item": "Items/Cap.fvdata" } ], "money": 12 },
                    "equipment": {},
                    "attributes": { "values": [ { "attribute": "Attribute.Skill.Stealth", "value": 4 }, { "attribute": "Attribute.Physical.Health", "value": 100 } ] } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Protagonist = spawned[0];
        HubViewModel hub = new(Game);
        Game.Lifecycle.Play(Game.World);
        Game.Events.Dispatch();
        return hub;
    }

    public void StartQuest(string id)
    {
        QuestSystem quests = Game.Services.GetService<QuestSystem>()!;
        quests.Start(new Tag(id));
        Game.Events.Dispatch();
    }

    public void Learn(string id)
    {
        KnowledgeSystem knowledge = Game.Services.GetService<KnowledgeSystem>()!;
        knowledge.Learn(new Tag(id));
        Game.Events.Dispatch();
    }

    public HubViewModelTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
