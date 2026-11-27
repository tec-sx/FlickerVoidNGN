using Arch.Core;

using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Props.Tests;

public sealed class PropEntityTests : IDisposable
{
    private readonly PropEntityTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsEachPartWithItsMeshAndExactCollisionAndTheDoorSwinging()
    {
        _builder.Write(PropFileTestsBuilder.WithDoor());

        Entity box = _builder.Place();

        World world = _builder.World;
        Entity first = _builder.Parts.Find(box, "part.1");
        world.Get<MeshRenderer>(first).Mesh.Should().Be("Props/Box.fvprop#part.1");
        world.Get<StaticBody>(first).Collider.Should().BeOfType<TriangleCollider>();
        _builder.Physics.HasBody(first).Should().BeTrue();
        Entity door = _builder.Parts.Find(box, "door.1");
        world.Get<RigidBody>(door).Kinematic.Should().BeTrue();
        world.Has<Toggle>(door).Should().BeTrue();
        _builder.Parts.FindAll(box).Should().HaveCount(3);
    }

    [Fact]
    public void ASavedFileBringsThePlacedPropInLine()
    {
        _builder.Write(PropFileTestsBuilder.WithDoor());
        Entity box = _builder.Place();
        PropFile changed = PropFile.Create();
        changed.AddBox(System.Numerics.Vector3.One, System.Numerics.Vector3.UnitX);

        _builder.Write(changed);
        _builder.Game.Tick(_builder.Game.Loop.FixedStep);

        _builder.Parts.FindAll(box).Should().HaveCount(2);
    }
}
