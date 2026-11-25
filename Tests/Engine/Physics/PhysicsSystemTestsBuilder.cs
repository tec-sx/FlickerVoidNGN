
using System.Text;
using System.Text.Json;

using Arch.Core;

using Microsoft.Extensions.Logging;

using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Physics.Tests;

/// <summary>Builds the physics system under test in a new game, which the test disposes.</summary>
internal sealed class PhysicsSystemTestsBuilder
{
    public PhysicsSystemTestsBuilder()
    {
        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        Game = new Game(loggerFactory);
    }

    public Game Game { get; }

    /// <summary>How high the top crate of <see cref="SpawnPile"/> starts.</summary>
    public const float PileTop = 12;

    public IPhysicsSystem Build() => Game.AddPhysics();

    /// <summary>Adds physics and spawns a floor with crates dropped onto it in a column, the top one last.</summary>
    public IReadOnlyList<Entity> SpawnPile()
    {
        Build();
        StringBuilder entities = new("""
            { "name": "Floor", "components": { "transform": {}, "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);
        for (int crate = 0; crate < 12; crate++)
        {
            float height = PileTop - crate;
            entities.Append($$""", { "name": "Crate {{crate}}", "components": { "transform": { "position": [0, {{height}}, 0] }, "rigidBody": { "collider": { "type": "box" } } } }""");
        }

        string json = $$"""{ "entities": [ {{entities}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        return [spawned[0], .. spawned.Skip(1).Reverse()];
    }

    public void Run(int seconds)
    {
        int steps = seconds * 60;
        for (int i = 0; i < steps; i++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }
}
