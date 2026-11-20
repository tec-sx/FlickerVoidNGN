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
