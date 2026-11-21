using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

/// <summary>Builds a playing game with FlickerVoid's gameplay and spawns levers to use, each with its own definition.</summary>
internal sealed class InteractableHighlightSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;
    private int _definitions;

    public InteractableHighlightSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public void Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddRendering();
        Game.AddFlow();
        FlowGraphs graphs = new(content, Game.Data);
        Game.Services.AddService(graphs);
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        Game.Lifecycle.Play(Game.World);
    }

    public Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> entities = Game.Spawner.Spawn(scene, Game.World);
        return entities[0];
    }

    /// <summary>A lever with a Use offer, which only interactors of <paramref name="compatible"/> may use when it is given.</summary>
    public Entity SpawnInteractable(Vector3 position, string? compatible = null)
    {
        _definitions++;
        string file = $"Lever{_definitions}.fvdata";
        string interactors = "";
        if (compatible is not null)
        {
            interactors = $$""", "compatibleInteractors": [ "{{compatible}}" ]""";
        }

        string definition = $$"""
            { "type": "interactable", "offers": [ { "input": "Input.Use", "action": "Interaction.Action.Pull", "display": { "name": "Pull" } } ]{{interactors}} }
            """;
        string fullPath = Path.Combine(_folder, file);
        File.WriteAllText(fullPath, definition);
        string entity = $$"""
            { "name": "Lever", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] },
              "interactable": { "definition": "{{file}}" } } }
            """;
        return Spawn(entity);
    }

    public void Run()
    {
        Game.Tick(Game.Loop.FixedStep);
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
