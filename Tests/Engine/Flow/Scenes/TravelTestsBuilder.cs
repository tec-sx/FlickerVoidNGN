using System.Text.Json.Nodes;

using Arch.Core;

using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Flow.Tests.Scenes;

/// <summary>
/// Builds a game with flow that has travelled to the street, in a temporary content folder with the street and the cellar, whose
/// stairs are an entry; the fox travels between them.
/// </summary>
internal sealed class TravelTestsBuilder : IDisposable
{
    public const string Street = "Scenes/Street.fvscene";
    public const string Cellar = "Scenes/Cellar.fvscene";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TravelTestsBuilder()
    {
        WriteFile(Street, """{ "entities": [ { "name": "Fox", "components": { "transform": { "position": [3, 0, 0] }, "traveller": {} } } ] }""");
        WriteFile(Cellar, """
            { "entities": [
                { "name": "Stairs", "components": { "transform": { "position": [9, -3, 0] } } },
                { "name": "Fox", "components": { "transform": {}, "traveller": {} } }
            ] }
            """);
        Game.AddFlow();
        ContentManager content = new(_folder);
        SceneLoader scenes = new(Game.Spawner, content, Game.World, NullLogger<SceneLoader>.Instance);
        Travel = Game.AddSceneTravel(scenes);
        Travel.Travel(Street);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public SceneTravel Travel { get; }

    public ConditionContext Context => new(Game.Facts) { Services = Game.Services };

    /// <summary>Starts a run of the graph written as JSON, read with the node types flow registers.</summary>
    public FlowRun Run(string json)
    {
        JsonObject file = JsonNode.Parse(json)!.AsObject();
        FlowGraph graph = FlowGraphs.Read("Test.fvflow", file, Game.Data);
        FlowRun run = new(graph, Context);
        run.Start();
        return run;
    }

    /// <summary>Where the fox stands, in whichever scene is loaded.</summary>
    public System.Numerics.Vector3 FoxPosition()
    {
        System.Numerics.Vector3 position = default;
        QueryDescription travellers = new QueryDescription().WithAll<Traveller, Transform>();
        Game.World.Query(in travellers, (ref Transform transform) => position = transform.Position);
        return position;
    }

    public void Tick() => Game.Tick(Game.Loop.FixedStep);

    public void Dispose()
    {
        Game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private void WriteFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
    }
}
