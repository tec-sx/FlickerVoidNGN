using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Gameplay.Tests.World.Society.Residents;

/// <summary>
/// Builds a playing game with FlickerVoid's gameplay and no window, a floor 20 metres wide whose top is at 0 and a resident at the
/// origin facing +Z that sees all around it, and records where the resident has been.
/// </summary>
internal sealed class ResidentSystemTestsBuilder : IDisposable
{
    private const string Floor = """
        { "name": "Floor", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [20, 0.2, 20] } } } }
        """;

    /// <summary>A flow with a single line that waits for the player.</summary>
    private const string Greeting = """
        { "nodes": [ { "$type": "start", "id": "start" }, { "$type": "line", "id": "hello", "text": "Welcome.", "waitForInput": true } ],
          "links": [ { "from": "start", "to": "hello" } ] }
        """;

    private readonly string _folder;
    private DataAssets? _data;

    public ResidentSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        string greeting = Path.Combine(_folder, "Greeting.fvflow");
        File.WriteAllText(greeting, Greeting);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Resident { get; private set; }

    public List<Vector3> Positions { get; } = [];

    /// <summary>Starts play with a resident that wanders the given radius around its home and rests the given seconds between walks.</summary>
    public ResidentSystemTestsBuilder Build(float wanderRadius, float rest = 1)
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
        Spawn(Floor);
        string resident = $$"""
            { "name": "Innkeeper", "components": { "transform": {}, "characterMover": {}, "perception": { "sightAngle": 180 },
              "resident": { "wanderRadius": {{wanderRadius}}, "rest": {{rest}} },
              "dialogueParticipant": { "speaker": "Character.Innkeeper", "dialogue": "Greeting.fvflow" } } }
            """;
        Resident = Spawn(resident);
        return this;
    }

    public Entity AddFox(Vector3 position)
    {
        string json = $$"""{ "name": "Fox", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "perceivable": {} } }""";
        return Spawn(json);
    }

    public Vector3 Forward()
    {
        Quaternion rotation = Game.World.Get<Transform>(Resident).Rotation;
        return Vector3.Transform(Vector3.UnitZ, rotation);
    }

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
            Vector3 position = Game.World.Get<Transform>(Resident).Position;
            Positions.Add(position);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        return spawned[0];
    }
}
