using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Interaction.Interactors;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Story.Quests;
using FlickerVoid.Gameplay;
using FlickerVoid.Interface.Hud;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Interface.Tests.Hud;

/// <summary>
/// Builds the HUD over a playing game with FlickerVoid's gameplay and no window: the cellar quest, an innkeeper with a Talk offer
/// and a short dialogue (a greeting that waits, a choice of two, an answer), and the protagonist.
/// </summary>
internal sealed class HudViewModelTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;
    private int _extraQuests;

    public HudViewModelTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Quests/Cellar.fvdata", """
            { "type": "quest", "id": "Quest.Cellar", "display": { "name": "The cellar key" },
              "objectives": [
                { "fact": "Quest.Cellar.FoundKey", "description": "Find the cellar key", "completeWhen": { "conditions": [ { "$type": "fact", "fact": "Has.Key" } ] } },
                { "fact": "Quest.Cellar.ReturnedKey", "description": "Tell the innkeeper", "visibleWhen": { "conditions": [ { "$type": "fact", "fact": "Quest.Cellar.FoundKey" } ] } } ] }
            """);
        WithFile("Interactables/Talk.fvdata", """
            { "type": "interactable", "id": "Interactable.Talk", "display": { "name": "Person" },
              "offers": [ { "input": "Input.Use", "action": "Interaction.Action.Talk", "display": { "name": "Talk" },
                            "effects": { "effects": [ { "$type": "startConversation" } ] } } ] }
            """);
        WithFile("Dialogues/Innkeeper.fvflow", """
            { "nodes": [
                { "$type": "start", "id": "start" },
                { "$type": "line", "id": "greet", "text": "Evening, fox.", "waitForInput": true },
                { "$type": "choice", "id": "ask", "options": [ { "text": "Anything I can do for you?" }, { "text": "Goodbye." } ] },
                { "$type": "line", "id": "job", "text": "Find my key.", "waitForInput": true } ],
              "links": [
                { "from": "start", "to": "greet" }, { "from": "greet", "to": "ask" },
                { "from": "ask", "output": "First", "to": "job" } ] }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public QuestSystem Quests => Game.Services.GetService<QuestSystem>()!;

    public Entity Protagonist { get; private set; }

    public Entity Innkeeper { get; private set; }

    /// <summary>Adds quests named "Extra 1" and on, with no objectives.</summary>
    public HudViewModelTestsBuilder WithQuests(int count)
    {
        _extraQuests = count;
        return this;
    }

    public HudViewModel Build()
    {
        for (int index = 1; index <= _extraQuests; index++)
        {
            WithFile($"Quests/Extra{index}.fvdata", $$"""{ "type": "quest", "id": "Quest.Extra{{index}}", "display": { "name": "Extra {{index}}" } }""");
        }

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
                { "name": "Fox", "components": { "transform": {}, "protagonist": {}, "interactor": { "reach": 2 } } },
                { "name": "Innkeeper", "components": { "transform": { "position": [1, 0, 0] }, "interactable": { "definition": "Interactables/Talk.fvdata" },
                    "dialogueParticipant": { "speaker": "Character.Innkeeper", "dialogue": "Dialogues/Innkeeper.fvflow" } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Protagonist = spawned[0];
        Innkeeper = spawned[1];
        HudViewModel hud = new(Game);
        Game.Lifecycle.Play(Game.World);
        Game.Events.Dispatch();
        return hud;
    }

    public void StartQuest(string id)
    {
        Quests.Start(new Tag(id));
        Game.Events.Dispatch();
    }

    /// <summary>The protagonist starts talking to the innkeeper, as its Talk offer does.</summary>
    public void Talk()
    {
        DialogueSystem dialogue = Game.Services.GetService<DialogueSystem>()!;
        dialogue.Start(Protagonist, Innkeeper);
        Game.Events.Dispatch();
    }

    public HudViewModelTestsBuilder WithFile(string path, string json)
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
