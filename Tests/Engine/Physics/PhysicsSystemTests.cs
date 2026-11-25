using System.Numerics;
using System.Text.Json;

using Arch.Core;

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
    public void RayCastsCanPassThroughTheBodyTheyStartIn()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Guard", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [1, 2, 1], "offset": [0, 1, 0] } } } },
            { "name": "Crate", "components": {
                "transform": { "position": [0, 1.5, 4] },
                "rigidBody": { "kinematic": true, "collider": { "type": "box" } } } }
            """);
        Step();
        Ray ahead = new(new Vector3(0, 1.5f, 0), Vector3.UnitZ);

        bool hitSelf = _physics.RayCast(ahead, 100, out RayHit self);
        bool hitCrate = _physics.RayCast(ahead, 100, entities[0], out RayHit crate);

        hitSelf.Should().BeTrue();
        self.Entity.Should().Be(entities[0]);
        hitCrate.Should().BeTrue();
        crate.Entity.Should().Be(entities[1]);
        crate.Distance.Should().BeApproximately(3.5f, 1e-3f);
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
    public void BodiesAtRestOnStaticBodiesThatStayPutFallAsleep()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 2, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box" } } } },
            { "name": "Floor", "components": {
                "transform": { "rotation": [0, 0.2588, 0, 0.9659] },
                "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);

        Run(seconds: 4);

        bool awake = _physics.IsAwake(entities[0]);
        awake.Should().BeFalse("nothing moved the floor, so nothing woke the crate");
    }

    [Fact]
    public void BodiesRestingOnAStaticBodyWakeWhenItMovesAway()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 2, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box" } } } },
            { "name": "Shelf", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [4, 1, 4] } } } }
            """);
        Run(seconds: 4);
        bool asleepOnShelf = !_physics.IsAwake(entities[0]);

        _game.World.Get<Transform>(entities[1]).Position = new Vector3(50, 0, 0);
        Run(seconds: 1);

        asleepOnShelf.Should().BeTrue();
        Transform crate = _game.World.Get<Transform>(entities[0]);
        crate.Position.Y.Should().BeLessThan(0, "the shelf moved out from under it, so it fell");
    }

    [Fact]
    public void FallingBodiesAreAwakeAndStaticBodiesNeverAre()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": { "transform": { "position": [0, 10, 0] }, "rigidBody": { "collider": { "type": "sphere" } } } },
            { "name": "Floor", "components": { "transform": {}, "staticBody": { "collider": { "type": "box" } } } },
            { "name": "Marker", "components": { "transform": {} } }
            """);

        Step();

        bool ballAwake = _physics.IsAwake(entities[0]);
        bool floorAwake = _physics.IsAwake(entities[1]);
        bool markerAwake = _physics.IsAwake(entities[2]);
        ballAwake.Should().BeTrue();
        floorAwake.Should().BeFalse();
        markerAwake.Should().BeFalse();
    }

    [Fact]
    public void SolidGreyboxesBlockWhilePlaying()
    {
        _game.Lifecycle.Play(_game.World);
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 5, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box", "offset": [0, 0.5, 0] } } } },
            { "name": "Floor", "components": {
                "transform": { "position": [0, -1, 0] },
                "greybox": { "size": [20, 1, 20] } } },
            { "name": "Rug", "components": {
                "transform": { "position": [0, 0, 0] },
                "greybox": { "size": [2, 0.1, 2], "solid": false } } }
            """);

        Run(seconds: 3);

        Transform crate = _game.World.Get<Transform>(entities[0]);
        crate.Position.Y.Should().BeInRange(-0.05f, 0.05f);
    }

    [Fact]
    public void ShapesSitAtTheirCollidersOffsetInTheEntitysSpace()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Door", "components": {
                "transform": { "position": [3, 0, 0], "rotation": [0, 0.7071068, 0, 0.7071068] },
                "staticBody": { "collider": { "type": "box", "size": [1, 2, 0.1], "offset": [0.5, 1, 0] } } } },
            { "name": "Crate", "components": {
                "transform": { "position": [0, 5, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box", "offset": [0, 0.5, 0] } } } },
            { "name": "Floor", "components": {
                "transform": { "position": [0, -0.5, 0] },
                "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);
        Step();
        Ray atPanel = new(new Vector3(3, 1, 5), -Vector3.UnitZ);

        bool hit = _physics.RayCast(atPanel, 100, out RayHit panel);
        Run(seconds: 3);

        hit.Should().BeTrue();
        panel.Entity.Should().Be(entities[0]);
        panel.Point.Z.Should().BeApproximately(0, 1e-3f);
        Transform crate = _game.World.Get<Transform>(entities[1]);
        crate.Position.Y.Should().BeInRange(-0.05f, 0.05f);
    }

    [Fact]
    public void HullsCollideAsTheShapeTheirPointsSpanInTheEntitysSpace()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 4, 0] },
                "rigidBody": { "collider": { "type": "hull", "points": [
                    [-0.5, 0, -0.5], [0.5, 0, -0.5], [0.5, 0, 0.5], [-0.5, 0, 0.5],
                    [-0.5, 1, -0.5], [0.5, 1, -0.5], [0.5, 1, 0.5], [-0.5, 1, 0.5]] } } } },
            { "name": "Floor", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);

        Run(seconds: 3);

        Transform crate = _game.World.Get<Transform>(entities[0]);
        crate.Position.Y.Should().BeInRange(0.45f, 0.55f);
    }

    [Fact]
    public void TrianglesWoundAsTheEnginesModelsStandFacingTheirFront()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": {
                "transform": { "position": [0, 3, 0] },
                "rigidBody": { "collider": { "type": "sphere" } } } },
            { "name": "Ground", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "triangles",
                    "positions": [[-10, 0, -10], [10, 0, -10], [10, 0, 10], [-10, 0, 10]], "indices": [0, 1, 2, 0, 2, 3] } } } }
            """);
        Step();
        Ray down = new(new Vector3(4, 10, 4), -Vector3.UnitY);

        bool hit = _physics.RayCast(down, 100, entities[0], out RayHit ground);
        Run(seconds: 3);

        hit.Should().BeTrue();
        ground.Entity.Should().Be(entities[1]);
        ground.Point.Y.Should().BeApproximately(0, 1e-3f);
        ground.Normal.Y.Should().BeApproximately(1, 1e-3f);
        Transform ball = _game.World.Get<Transform>(entities[0]);
        ball.Position.Y.Should().BeInRange(0.45f, 0.55f);
    }

    [Fact]
    public void MovingBodiesRefuseTriangles()
    {
        Spawn("""
            { "name": "Platform", "components": {
                "transform": {},
                "rigidBody": { "kinematic": true, "collider": { "type": "triangles",
                    "positions": [[0, 0, 0], [1, 0, 0], [0, 0, 1]], "indices": [0, 1, 2] } } } }
            """);

        Action step = Step;

        step.Should().Throw<InvalidOperationException>().WithMessage("*'Platform'*hull*");
    }

    [Theory]
    [InlineData("[[0, 0, 0], [1, 0, 0], [2, 0, 0], [3, 0, 0]]")]
    [InlineData("[[0, 0, 0], [1, 0, 0], [0, 0, 1], [1, 0, 1]]")]
    [InlineData("[[0, 0, 0], [1, 0, 0], [0, 1, 0]]")]
    public void RefusesAHullOfPointsOnALineOrAPlane(string points)
    {
        Spawn($$"""
            { "name": "Sheet", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "hull", "points": {{points}} } } } }
            """);

        Action step = Step;

        step.Should().Throw<InvalidOperationException>().WithMessage("*'Sheet'*hull*");
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
    public void KinematicBodiesFollowTheirTransform()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Innkeeper", "components": {
                "transform": {},
                "rigidBody": { "kinematic": true, "collider": { "type": "box", "size": [0.6, 1.7, 0.4], "offset": [0, 0.85, 0] } } } }
            """);
        Step();

        _game.World.Get<Transform>(entities[0]).Position = new Vector3(4, 0, 0);
        Step();
        bool hitWhereItWas = _physics.RayCast(new Ray(new Vector3(0, 10, 0), -Vector3.UnitY), 100, out _);
        bool hitWhereItIs = _physics.RayCast(new Ray(new Vector3(4, 10, 0), -Vector3.UnitY), 100, out RayHit hit);

        hitWhereItWas.Should().BeFalse();
        hitWhereItIs.Should().BeTrue();
        hit.Point.Y.Should().BeApproximately(1.7f, 1e-3f);
        _game.World.Get<Transform>(entities[0]).Position.Should().Be(new Vector3(4, 0, 0));
    }

    [Fact]
    public void KinematicBodiesTurnAboutTheirEntityWithItsTransform()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Door", "components": {
                "transform": {},
                "rigidBody": { "kinematic": true, "collider": { "type": "box", "size": [1, 2, 0.1], "offset": [0.5, 1, 0] } } } }
            """);
        Step();

        _game.World.Get<Transform>(entities[0]).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Step();
        bool hitClosed = _physics.RayCast(new Ray(new Vector3(0.5f, 1, 5), -Vector3.UnitZ), 100, out _);
        bool hitOpen = _physics.RayCast(new Ray(new Vector3(5, 1, -0.5f), -Vector3.UnitX), 100, out RayHit open);

        hitClosed.Should().BeFalse();
        hitOpen.Should().BeTrue();
        open.Point.X.Should().BeApproximately(0.05f, 1e-3f);
    }

    [Fact]
    public void StaticBodiesMoveToATransformThatMoved()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 0.5, 0] },
                "staticBody": { "collider": { "type": "box" } } } }
            """);
        Step();

        _game.World.Get<Transform>(entities[0]).Position = new Vector3(3, 0.5f, 0);
        Step();
        bool hitWhereItWas = _physics.RayCast(new Ray(new Vector3(0, 10, 0), -Vector3.UnitY), 100, out _);
        bool hitWhereItIs = _physics.RayCast(new Ray(new Vector3(3, 10, 0), -Vector3.UnitY), 100, out RayHit hit);

        hitWhereItWas.Should().BeFalse();
        hitWhereItIs.Should().BeTrue();
        hit.Entity.Should().Be(entities[0]);
    }

    [Fact]
    public void GivesEachRigidAndStaticBodyABodyInTheSimulation()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Ball", "components": { "transform": {}, "rigidBody": { "collider": { "type": "sphere" } } } },
            { "name": "Floor", "components": { "transform": {}, "staticBody": { "collider": { "type": "box" } } } },
            { "name": "Marker", "components": { "transform": {} } }
            """);

        Step();

        bool ballHasBody = _physics.HasBody(entities[0]);
        bool floorHasBody = _physics.HasBody(entities[1]);
        bool markerHasBody = _physics.HasBody(entities[2]);
        ballHasBody.Should().BeTrue();
        floorHasBody.Should().BeTrue();
        markerHasBody.Should().BeFalse();
        _physics.BodyCount.Should().Be(2);
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

        _physics.BodyCount.Should().Be(0);
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

        bool hasBody = _physics.HasBody(entities[0]);
        hasBody.Should().BeFalse();
        _physics.BodyCount.Should().Be(0);
    }

    [Fact]
    public void SleepingBodiesStillCount()
    {
        IReadOnlyList<Entity> entities = Spawn("""
            { "name": "Crate", "components": {
                "transform": { "position": [0, 2, 0] },
                "rigidBody": { "mass": 10, "collider": { "type": "box" } } } },
            { "name": "Floor", "components": {
                "transform": {},
                "staticBody": { "collider": { "type": "box", "size": [20, 1, 20] } } } }
            """);

        Run(seconds: 4);

        bool awake = _physics.IsAwake(entities[0]);
        bool hasBody = _physics.HasBody(entities[0]);
        awake.Should().BeFalse();
        hasBody.Should().BeTrue();
        _physics.BodyCount.Should().Be(2);
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
    public void GamesOnSeveralThreadsStepTheirPhysicsAtOnce()
    {
        PhysicsSystemTestsBuilder[] others = [new(), new(), new(), new()];
        IReadOnlyList<Entity>[] piles = [.. others.Select(other => other.SpawnPile())];

        Parallel.For(0, others.Length, index => others[index].Run(seconds: 2));

        for (int index = 0; index < others.Length; index++)
        {
            Game game = others[index].Game;
            Transform top = game.World.Get<Transform>(piles[index][^1]);
            top.Position.Y.Should().BeLessThan(PhysicsSystemTestsBuilder.PileTop, "every game stepped its own pile");
            game.Dispose();
        }
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
