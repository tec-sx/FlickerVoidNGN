using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Dialogue.Conversations;
using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Dialogue.Tests.Conversations;

/// <summary>Builds a playing game with flow and dialogue over dialogue graphs and bark tables in a temporary content folder.</summary>
internal sealed class DialogueSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public DialogueSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public DialogueSystem Dialogue { get; private set; } = null!;

    public SaveSystem Saves { get; private set; } = null!;

    public World World => Game.World;

    /// <summary>A dialogue graph at <c>Talk.fvflow</c> with these nodes and links, given as JSON arrays.</summary>
    public DialogueSystemTestsBuilder WithGraph(string nodes, string links, string path = "Talk.fvflow")
    {
        return WithFile(path, $$"""{ "nodes": [ {{nodes}} ], "links": [ {{links}} ] }""");
    }

    public DialogueSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Adds flow, dialogue and the content services, starts play and runs a first step.</summary>
    public DialogueSystemTestsBuilder Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddFlow();
        Game.Services.AddService(new FlowGraphs(content, Game.Data));
        DialogueSettings settings = new() { MinLineDuration = 1, SecondsPerCharacter = 0.01f };
        Dialogue = Game.AddDialogue(settings, new Random(7));
        string saves = Path.Combine(_folder, "Saves");
        Saves = Game.AddSaves(new SaveSettings { Folder = saves });
        Game.Lifecycle.Play(Game.World);
        Game.Tick(TimeSpan.Zero);
        return this;
    }

    /// <summary>Spawns a participant, its values given as scene JSON, with the tags given.</summary>
    public Entity Participant(string participant = "", params string[] tags)
    {
        string json = $$"""{ "entities": [ { "components": { "dialogueParticipant": { {{participant}} } } } ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Entity entity = spawned[0];
        List<Tag> owned = tags.Select(tag => new Tag(tag)).ToList();
        Game.World.Add(entity, new OwnedTags(owned));
        return entity;
    }

    /// <summary>The hero and an innkeeper whose dialogue is <c>Talk.fvflow</c>.</summary>
    public (Entity Hero, Entity Innkeeper) Pair()
    {
        Entity hero = Participant(""" "speaker": "Character.Hero" """);
        Entity innkeeper = Participant(""" "speaker": "Character.Innkeeper", "dialogue": "Talk.fvflow" """);
        return (hero, innkeeper);
    }

    /// <summary>A context with the game's facts and services between the two entities, as an interaction gives an effect.</summary>
    public ConditionContext Context(Entity instigator, Entity target)
    {
        WorldEntityView instigatorView = new(Game.World, instigator);
        WorldEntityView targetView = new(Game.World, target);
        return new ConditionContext(Game.Facts) { Services = Game.Services, Instigator = instigatorView, Target = targetView };
    }

    public T Read<T>(string json)
        where T : class
    {
        JsonTypeInfo typeInfo = Game.Data.Options.GetTypeInfo(typeof(T));
        return (T)JsonSerializer.Deserialize(json, typeInfo)!;
    }

    /// <summary>Lets <paramref name="seconds"/> of game time pass and dispatches what was sent.</summary>
    public void Wait(double seconds)
    {
        TimeSpan step = TimeSpan.FromSeconds(0.25);
        for (double passed = 0; passed < seconds; passed += step.TotalSeconds)
        {
            Game.Tick(step);
        }

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
