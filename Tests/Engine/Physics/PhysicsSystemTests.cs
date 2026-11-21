using System.Numerics;
using System.Text.Json;

using Arch.Core;

using BepuPhysics;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Geometry;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Physics.Tests;

public sealed class PhysicsSystemTests : IDisposable
{
    private readonly Game _game;
    private readonly IPhysicsSystem _physics;

    public PhysicsSystemTests()
    {
        PhysicsSystemTestsBuilder builder = new();
        _physics = builder.Build();
        _game = builder.Game;
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
        transform.Position.Y.Should().BeInRange(4.5f, 6);
    }

    [Fact]
    public void RayCastsFindTheNearestBodyAndWhereTheRayMetIt()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [2, 0.5, 0] },
                "rigidBody": { "kinematic": true, "collider": { "type": "box" } } } },
            { "name": "Floor", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [20, 0.2, 20] } } } }
            """);
        Step();
        Ray down = new(new Vector3(-3, 10, 0), -Vector3.UnitY);
        Ray onCrate = new(new Vector3(2, 10, 0), -Vector3.UnitY);

        bool hitFloor = _physics.RayCast(down, 100, out RayHit floor);
        bool hitCrate = _physics.RayCast(onCrate, 100, out RayHit crate);
        bool missed = _physics.RayCast(down, 5, out _);

        hitFloor.Should().BeTrue();
        floor.Entity.Should().Be(entities[1]);
        floor.Point.Y.Should().BeApproximately(0.1f, 1e-3f);
        floor.Normal.Y.Should().BeApproximately(1, 1e-3f);
        hitCrate.Should().BeTrue();
        crate.Entity.Should().Be(entities[0]);
        crate.Distance.Should().BeApproximately(9, 1e-3f);
        missed.Should().BeFalse();
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
        crate.Position.Y.Should().BeInRange(0.95f, 1.05f);
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
        transform.Position.Y.Should().Be(3);
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
        ballExists.Should().BeTrue();
        floorHasStaticHandle.Should().BeTrue();
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

        _physics.Simulation.Bodies.ActiveSet.Count.Should().Be(0);
        _physics.Simulation.Statics.Count.Should().Be(0);
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
        hasHandle.Should().BeFalse();
        _physics.Simulation.Bodies.ActiveSet.Count.Should().Be(0);
    }

    [Fact]
    public void ColliderWithoutATypeNamesTheEntity()
    {
        Action spawn = () => Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "radius": 1 } } } }
            """);

        spawn.Should().Throw<InvalidDataException>().WithMessage("*'Ball'*'rigidBody'*");
    }

    [Fact]
    public void RegistersItselfAsAService()
    {
        IPhysicsSystem? service = _game.Services.GetService<IPhysicsSystem>();

        service.Should().BeSameAs(_physics);
    }

    private IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData? scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData);
        scene.Should().NotBeNull();
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
