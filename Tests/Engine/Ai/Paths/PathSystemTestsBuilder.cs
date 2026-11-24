using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Ai.Tests.Paths;

/// <summary>
/// Builds a playing game with physics, characters and paths over static bodies written as scene JSON: a 10 metre floor whose top is at
/// 0, and a wall across the middle from z -3 to 3. It walks characters and records how far they strayed from the straight line.
/// </summary>
internal sealed class PathSystemTestsBuilder : IDisposable
{
    private const string Floor = """{ "name": "Floor", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [10, 0.2, 10] } } } }""";
    private const string LongFloor = """{ "name": "Floor", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [40, 0.2, 10] } } } }""";
    private const string Wall = """{ "name": "Wall", "components": { "transform": { "position": [0, 1, 0] }, "staticBody": { "collider": { "type": "box", "size": [0.4, 2, 6] } } } }""";

    private readonly List<string> _entities = [];

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public float MaxAbsZ { get; private set; }

    public PathSystemTestsBuilder WithFloor()
    {
        _entities.Add(Floor);
        return this;
    }

    /// <summary>A floor 40 metres along X, longer than a tile.</summary>
    public PathSystemTestsBuilder WithLongFloor()
    {
        _entities.Add(LongFloor);
        return this;
    }

    public PathSystemTestsBuilder WithWall()
    {
        _entities.Add(Wall);
        return this;
    }

    public PathSystem Build()
    {
        Game.AddPhysics();
        Game.AddCharacters();
        PathSystem paths = Game.AddAi();
        Spawn(_entities);
        Game.Lifecycle.Play(Game.World);
        return paths;
    }

    public Entity AddWall() => SpawnPlaying(Wall);

    /// <summary>A playing entity with a rigid body written as JSON, standing at the position.</summary>
    public Entity AddBody(string name, Vector3 position, string bodyJson, Quaternion? rotation = null)
    {
        return SpawnPlaying(name, position, rotation ?? Quaternion.Identity, $$""" "rigidBody": {{bodyJson}} """);
    }

    /// <summary>A playing entity with a static body written as JSON, standing at the position.</summary>
    public Entity AddStaticBody(string name, Vector3 position, string bodyJson)
    {
        return SpawnPlaying(name, position, Quaternion.Identity, $$""" "staticBody": {{bodyJson}} """);
    }

    public void Place(Entity entity, Vector3 position, Quaternion rotation)
    {
        ref Transform transform = ref Game.World.Get<Transform>(entity);
        transform.Position = position;
        transform.Rotation = rotation;
    }

    public Entity AddWalker(Vector3 position)
    {
        string json = $$"""{ "name": "Hero", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "characterMover": { "speed": 2 } } }""";
        return SpawnPlaying(json);
    }

    public void Remove(Entity entity) => Game.Lifecycle.Destroy(Game.World, entity);

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        QueryDescription movers = new QueryDescription().WithAll<Transform, Characters.Movement.CharacterMover>();
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
            Game.World.Query(in movers, (ref Transform transform) => MaxAbsZ = MathF.Max(MaxAbsZ, MathF.Abs(transform.Position.Z)));
        }
    }

    public void Dispose() => Game.Dispose();

    private Entity SpawnPlaying(string name, Vector3 position, Quaternion rotation, string componentJson)
    {
        string json = $$"""
            { "name": "{{name}}", "components": {
                "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}], "rotation": [{{rotation.X}}, {{rotation.Y}}, {{rotation.Z}}, {{rotation.W}}] },
                {{componentJson}} } }
            """;
        return SpawnPlaying(json);
    }

    private Entity SpawnPlaying(string entityJson)
    {
        Entity entity = Spawn([entityJson])[0];
        Game.Lifecycle.Start(Game.World, entity);
        return entity;
    }

    private IReadOnlyList<Entity> Spawn(IEnumerable<string> entities)
    {
        string list = string.Join(",", entities);
        string json = $$"""{ "entities": [ {{list}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World);
    }
}
