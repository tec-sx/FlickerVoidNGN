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

        BoxCollider boxCollider = Assert.IsType<BoxCollider>(box.Collider);
        SphereCollider sphereCollider = Assert.IsType<SphereCollider>(sphere.Collider);
        CapsuleCollider capsuleCollider = Assert.IsType<CapsuleCollider>(capsule.Collider);
        Vector3 boxSize = new(1, 2, 3);
        Assert.Equal(boxSize, boxCollider.Size);
        Assert.Equal(2, sphereCollider.Radius);
        Assert.Equal(0.25f, capsuleCollider.Radius);
        Assert.Equal(1.5f, capsuleCollider.Length);
    }

    [Fact]
    public void AcceptsTheTypeAfterOtherProperties()
    {
        RigidBody body = Read("""{ "collider": { "radius": 2, "type": "sphere" } }""");

        Assert.IsType<SphereCollider>(body.Collider);
    }

    [Fact]
    public void DefaultsToADynamicBodyOfOneKilogram()
    {
        RigidBody body = Read("""{ "collider": { "type": "sphere" } }""");

        Assert.Equal(1, body.Mass);
        Assert.False(body.Kinematic);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "collider": { "type": "cone" } }""")]
    [InlineData("""{ "collider": { "type": "sphere", "size": [1, 1, 1] } }""")]
    public void RejectsMissingOrUnknownColliders(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, PhysicsJsonContext.Default.RigidBody));
    }

    private static RigidBody Read(string json)
    {
        return JsonSerializer.Deserialize(json, PhysicsJsonContext.Default.RigidBody);
    }
}
