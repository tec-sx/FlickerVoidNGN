using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Engine.Creations.Tests;

public sealed class CreationPartsTests : IDisposable
{
    private const float Precision = 0.0001f;

    private readonly CreationPartsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsOnePartPerIdUnderTheRootAsItStarts()
    {
        _builder.Describe("body", new Vector3(0, 1, 0), "body").Describe("wheel.1L", new Vector3(-1, 0, 1), "wheel");

        Entity van = _builder.Place(new Vector3(10, 0, 0));

        Entity body = _builder.Parts.Find(van, "body");
        Entity wheel = _builder.Parts.Find(van, "wheel.1L");
        _builder.World.Get<CreationPart>(body).Should().Be(new CreationPart(van, "body"));
        _builder.World.Get<CreationPart>(wheel).Should().Be(new CreationPart(van, "wheel.1L"));
        _builder.Parts.FindAll(van).Should().Equal(body, wheel);
        _builder.Parts.Find(van, "wheel.9R").Should().Be(Entity.Null);
    }

    [Fact]
    public void PutsEachPartAtItsPlaceInTheRootsSpace()
    {
        Quaternion quarterTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        _builder.Describe("wheel.1L", new Vector3(1, 0, 0), "wheel");

        Entity van = _builder.Place(new Vector3(10, 0, 0), quarterTurn);

        Entity wheel = _builder.Parts.Find(van, "wheel.1L");
        Vector3 expected = new Vector3(10, 0, 0) + Vector3.Transform(Vector3.UnitX, quarterTurn);
        Vector3 position = _builder.PositionOf(wheel);
        position.X.Should().BeApproximately(expected.X, Precision);
        position.Z.Should().BeApproximately(expected.Z, Precision);
    }

    [Fact]
    public void PartsFollowTheRoot()
    {
        _builder.Describe("body", new Vector3(0, 1, 0), "body");
        Entity van = _builder.Place(Vector3.Zero);

        _builder.Move(van, new Vector3(0, 0, 5));
        _builder.Hierarchy.Update(_builder.World);

        Entity body = _builder.Parts.Find(van, "body");
        _builder.PositionOf(body).Should().Be(new Vector3(0, 1, 5));
    }

    [Fact]
    public void StartsEachPartWithTheComponentsItsKindGivesIt()
    {
        _builder.Describe("body", Vector3.Zero, "body").Describe("wheel.1L", Vector3.Zero, "wheel");

        Entity van = _builder.Place(Vector3.Zero);

        Entity wheel = _builder.Parts.Find(van, "wheel.1L");
        _builder.World.Get<TestLook>(wheel).Should().Be(new TestLook("wheel"));
        _builder.World.Has<Attached>(wheel).Should().BeTrue();
        _builder.Attached.Should().Equal("body", "wheel");
    }

    [Fact]
    public void PatchKeepsThePartsWhoseIdStaysAndAddsAndRemovesTheRest()
    {
        _builder.Describe("body", Vector3.Zero, "body").Describe("wheel.1L", Vector3.Zero, "wheel").Describe("wheel.1R", Vector3.Zero, "wheel");
        Entity van = _builder.Place(Vector3.Zero);
        Entity body = _builder.Parts.Find(van, "body");
        Entity left = _builder.Parts.Find(van, "wheel.1L");
        Entity right = _builder.Parts.Find(van, "wheel.1R");

        PartData[] parts = [CreationPartsTestsBuilder.Part("body", Vector3.Zero, "body"), CreationPartsTestsBuilder.Part("wheel.1L", Vector3.Zero, "wheel"), CreationPartsTestsBuilder.Part("wheel.2L", Vector3.Zero, "wheel")];
        _builder.Parts.Patch(van, parts);

        _builder.Parts.Find(van, "body").Should().Be(body);
        _builder.Parts.Find(van, "wheel.1L").Should().Be(left);
        _builder.World.IsAlive(right).Should().BeFalse();
        Entity added = _builder.Parts.Find(van, "wheel.2L");
        added.Should().NotBe(Entity.Null);
        _builder.World.Get<CreationPart>(added).Should().Be(new CreationPart(van, "wheel.2L"));
        _builder.Parts.FindAll(van).Should().Equal(body, left, added);
    }

    [Fact]
    public void PatchRestartsOnlyTheComponentsThatChanged()
    {
        _builder.Describe("body", Vector3.Zero, "box").Describe("wheel.1L", Vector3.Zero, "wheel");
        Entity van = _builder.Place(Vector3.Zero);
        _builder.Attached.Clear();

        PartData[] parts = [CreationPartsTestsBuilder.Part("body", Vector3.Zero, "rounded"), CreationPartsTestsBuilder.Part("wheel.1L", Vector3.Zero, "wheel")];
        _builder.Parts.Patch(van, parts);

        Entity body = _builder.Parts.Find(van, "body");
        _builder.World.Get<TestLook>(body).Should().Be(new TestLook("rounded"));
        _builder.Detached.Should().Equal("box");
        _builder.Attached.Should().Equal("rounded");
    }

    [Fact]
    public void PatchMovesAPartOnlyWhenItsPlaceChanged()
    {
        _builder.Describe("body", Vector3.Zero, "body").Describe("door", new Vector3(1, 0, 0), "door");
        Entity van = _builder.Place(Vector3.Zero);
        Entity door = _builder.Parts.Find(van, "door");
        _builder.Move(door, new Vector3(1, 0, 2));
        _builder.Hierarchy.Update(_builder.World);

        PartData[] parts = [CreationPartsTestsBuilder.Part("body", new Vector3(0, 1, 0), "body"), CreationPartsTestsBuilder.Part("door", new Vector3(1, 0, 0), "door")];
        _builder.Parts.Patch(van, parts);
        _builder.Hierarchy.Update(_builder.World);

        Entity body = _builder.Parts.Find(van, "body");
        _builder.PositionOf(body).Should().Be(new Vector3(0, 1, 0));
        _builder.PositionOf(door).Should().Be(new Vector3(1, 0, 2));
    }

    [Fact]
    public void PatchTakesOffComponentsAPartNoLongerHas()
    {
        Transform place = new();
        TestLook look = new("body");
        TestSize size = new(2);
        PartData sized = new PartData("body", place).With(look).With(size);
        _builder.Redescribe(sized);
        Entity van = _builder.Place(Vector3.Zero);

        PartData plain = new PartData("body", place).With(look);
        _builder.Parts.Patch(van, [plain]);

        Entity body = _builder.Parts.Find(van, "body");
        _builder.World.Has<TestSize>(body).Should().BeFalse();
        _builder.World.Get<TestLook>(body).Should().Be(look);
        _builder.Detached.Should().BeEmpty();
    }

    [Fact]
    public void RefusesTwoPartsWithOneIdAndChangesNothing()
    {
        _builder.Describe("body", Vector3.Zero, "body");
        Entity van = _builder.Place(Vector3.Zero);
        Entity body = _builder.Parts.Find(van, "body");

        PartData[] parts = [CreationPartsTestsBuilder.Part("wheel.1L", Vector3.Zero, "wheel"), CreationPartsTestsBuilder.Part("wheel.1L", Vector3.Zero, "wheel")];
        Action patch = () => _builder.Parts.Patch(van, parts);

        patch.Should().Throw<ArgumentException>().WithMessage("*two parts have the id 'wheel.1L'*");
        _builder.Parts.FindAll(van).Should().Equal(body);
    }

    [Fact]
    public void DestroyingTheRootDestroysItsParts()
    {
        _builder.Describe("body", Vector3.Zero, "body").Describe("wheel.1L", Vector3.Zero, "wheel");
        Entity van = _builder.Place(Vector3.Zero);
        Entity body = _builder.Parts.Find(van, "body");
        Entity wheel = _builder.Parts.Find(van, "wheel.1L");

        _builder.Spawner.Lifecycle.Destroy(_builder.World, van);

        _builder.World.IsAlive(body).Should().BeFalse();
        _builder.World.IsAlive(wheel).Should().BeFalse();
        _builder.Detached.Should().Equal("wheel", "body");
        _builder.Parts.FindAll(van).Should().BeEmpty();
    }

    [Fact]
    public void SavingTheLevelWritesNoParts()
    {
        _builder.Describe("body", Vector3.Zero, "body").Describe("wheel.1L", Vector3.Zero, "wheel");
        TestCreation van = new() { File = "Vans/Van.fvtest" };
        JsonElement creation = JsonSerializer.SerializeToElement(van, CreationsTestsJsonContext.Default.TestCreation);
        EntityData data = new() { Name = "Van", Components = { ["creation"] = creation } };
        SceneData level = new() { Entities = [data] };
        IReadOnlyList<Entity> entities = _builder.Spawner.Spawn(level, _builder.World);

        SceneData saved = _builder.Spawner.Capture(level, entities, _builder.World);

        _builder.Parts.FindAll(entities[0]).Should().HaveCount(2);
        saved.Entities.Should().ContainSingle().Which.Name.Should().Be("Van");
    }
}
