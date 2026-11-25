using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Physics.Colliders;

namespace FlickerVoid.Engine.Physics.Tests.Colliders;

public class ColliderJsonTests
{
    [Fact]
    public void ReadsEachColliderType()
    {
        RigidBody box = Read("""{ "collider": { "type": "box", "size": [1, 2, 3] } }""");
        RigidBody sphere = Read("""{ "collider": { "type": "sphere", "radius": 2 } }""");
        RigidBody capsule = Read("""{ "collider": { "type": "capsule", "radius": 0.25, "length": 1.5 } }""");

        Vector3 boxSize = new(1, 2, 3);
        box.Collider.Should().BeOfType<BoxCollider>().Which.Size.Should().Be(boxSize);
        sphere.Collider.Should().BeOfType<SphereCollider>().Which.Radius.Should().Be(2);
        CapsuleCollider capsuleCollider = capsule.Collider.Should().BeOfType<CapsuleCollider>().Subject;
        capsuleCollider.Radius.Should().Be(0.25f);
        capsuleCollider.Length.Should().Be(1.5f);
    }

    [Fact]
    public void ReadsHullsAndTriangles()
    {
        RigidBody hull = Read("""{ "collider": { "type": "hull", "points": [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]] } }""");
        StaticBody triangles = JsonSerializer.Deserialize(
            """{ "collider": { "type": "triangles", "positions": [[0, 0, 0], [1, 0, 0], [0, 0, 1]], "indices": [0, 2, 1] } }""",
            PhysicsJsonContext.Default.StaticBody);

        hull.Collider.Should().BeOfType<HullCollider>().Which.Points.Should().HaveCount(4).And.Contain(Vector3.UnitY);
        TriangleCollider triangleCollider = triangles.Collider.Should().BeOfType<TriangleCollider>().Subject;
        triangleCollider.Positions.Should().Equal(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ);
        triangleCollider.Indices.Should().Equal(0, 2, 1);
    }

    [Fact]
    public void AcceptsTheTypeAfterOtherProperties()
    {
        RigidBody body = Read("""{ "collider": { "radius": 2, "type": "sphere" } }""");

        body.Collider.Should().BeOfType<SphereCollider>();
    }

    [Fact]
    public void DefaultsToADynamicBodyOfOneKilogram()
    {
        RigidBody body = Read("""{ "collider": { "type": "sphere" } }""");

        body.Mass.Should().Be(1);
        body.Kinematic.Should().BeFalse();
    }

    [Fact]
    public void PathsGoAroundStaticBodiesButNotRigidOnesUnlessTheySaySo()
    {
        StaticBody wall = JsonSerializer.Deserialize("""{ "collider": { "type": "box" } }""", PhysicsJsonContext.Default.StaticBody);
        StaticBody key = JsonSerializer.Deserialize("""{ "collider": { "type": "box" }, "obstacle": false }""", PhysicsJsonContext.Default.StaticBody);
        RigidBody innkeeper = Read("""{ "kinematic": true, "collider": { "type": "box" } }""");
        RigidBody door = Read("""{ "kinematic": true, "obstacle": true, "collider": { "type": "box" } }""");

        wall.Obstacle.Should().BeTrue();
        key.Obstacle.Should().BeFalse();
        innkeeper.Obstacle.Should().BeFalse();
        door.Obstacle.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "collider": { "type": "cone" } }""")]
    [InlineData("""{ "collider": { "type": "sphere", "size": [1, 1, 1] } }""")]
    public void RejectsMissingOrUnknownColliders(string json)
    {
        Action read = () => JsonSerializer.Deserialize(json, PhysicsJsonContext.Default.RigidBody);

        read.Should().Throw<JsonException>();
    }

    private static RigidBody Read(string json)
    {
        return JsonSerializer.Deserialize(json, PhysicsJsonContext.Default.RigidBody);
    }
}
