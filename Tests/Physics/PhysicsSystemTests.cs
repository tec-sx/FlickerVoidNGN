using System.Text.Json;

using Arch.Core;

using BepuPhysics;

using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Physics.Tests;

public sealed class PhysicsSystemTests : IDisposable
{
    private readonly Game _game = new();
    private readonly PhysicsSystem _physics;

    public PhysicsSystemTests()
    {
        _physics = _game.AddPhysics();
    }

    public void Dispose()
    {
        _game.Dispose();
    }

    [Fact]
    public void BodiesFallUnderGravity()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": {
                "transform": { "position": [0, 10, 0] },
                "rigidBody": { "collider": { "type": "sphere" } } } }
            """);

        Run(seconds: 1);

        Transform transform = _game.World.Get<Transform>(entities[0]);
        Assert.InRange(transform.Position.Y, 4.5f, 6);
    }

    [Fact]
    public void BodiesComeToRestOnStaticBodies()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 5, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box" } } } },
            { "name": "Floor", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);

        Run(seconds: 3);

        Transform crate = _game.World.Get<Transform>(entities[0]);
        Assert.InRange(crate.Position.Y, 0.95f, 1.05f);
    }

    [Fact]
    public void KinematicBodiesStayWhereTheyAre()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Platform", "components": {
                "transform": { "position": [0, 3, 0] },
                "rigidBody": { "kinematic": true, "collider": { "type": "box" } } } }
            """);

        Run(seconds: 1);

        Transform transform = _game.World.Get<Transform>(entities[0]);
        Assert.Equal(3, transform.Position.Y);
    }

    [Fact]
    public void GivesEachBodyAHandleToItsSimulationBody()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "type": "sphere" } } } },
            { "name": "Floor", "components": { "transform": {}, "staticBody": { "collider": { "type": "box" } } } }
            """);

        Step();

        BodyHandle ball = _game.World.Get<BodyHandle>(entities[0]);
        bool floorHasStaticHandle = _game.World.Has<StaticHandle>(entities[1]);
        bool ballExists = _physics.Simulation.Bodies.BodyExists(ball);
        Assert.True(ballExists);
        Assert.True(floorHasStaticHandle);
    }

    [Fact]
    public void DestroyedEntitiesLeaveTheSimulation()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "type": "sphere" } } } },
            { "name": "Floor", "components": { "transform": {}, "staticBody": { "collider": { "type": "box" } } } }
            """);
        Step();

        _game.World.Destroy(entities[0]);
        _game.World.Destroy(entities[1]);
        Step();

        Assert.Equal(0, _physics.Simulation.Bodies.ActiveSet.Count);
        Assert.Equal(0, _physics.Simulation.Statics.Count);
    }

    [Fact]
    public void RemovingTheComponentRemovesTheBody()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "type": "sphere" } } } }
            """);
        Step();

        _game.World.Remove<RigidBody>(entities[0]);
        Step();

        bool hasHandle = _game.World.Has<BodyHandle>(entities[0]);
        Assert.False(hasHandle);
        Assert.Equal(0, _physics.Simulation.Bodies.ActiveSet.Count);
    }

    [Fact]
    public void ColliderWithoutATypeNamesTheEntity()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "radius": 1 } } } }
            """));

        Assert.Contains("'Ball'", exception.Message);
        Assert.Contains("'rigidBody'", exception.Message);
    }

    [Fact]
    public void RegistersItselfAsAService()
    {
        PhysicsSystem? service = _game.Services.GetService<PhysicsSystem>();

        Assert.Same(_physics, service);
    }

    private IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        Assert.NotNull(scene);
        return _game.Spawner.Spawn(scene, _game.World);
    }

    private void Step()
    {
        _game.Tick(_game.Loop.FixedStep);
    }

    private void Run(int seconds)
    {
        int steps = seconds * 60;
        for (int i = 0; i < steps; i++)
        {
            Step();
        }
    }
}
