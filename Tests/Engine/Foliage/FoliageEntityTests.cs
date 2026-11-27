using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Physics.Colliders;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Foliage.Tests;

public sealed class FoliageEntityTests : IDisposable
{
    private readonly FoliageEntityTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpawnsTheFilesPartsUnderThePlantWithASolidTrunk()
    {
        _builder.Write(FoliageFile.Create(FoliageKind.Tree));

        Entity oak = _builder.PlacePlant();

        World world = _builder.World;
        _builder.Parts.FindAll(oak).Should().HaveCount(10);
        Entity trunk = _builder.Parts.Find(oak, "trunk");
        world.Get<MeshRenderer>(trunk).Mesh.Should().Be("Foliage/Oak.fvfoliage#trunk");
        world.Get<StaticBody>(trunk).Collider.Should().BeOfType<TriangleCollider>();
        _builder.Physics.HasBody(trunk).Should().BeTrue();
        Entity crown = _builder.Parts.Find(oak, "crown.1");
        world.Get<MeshRenderer>(crown).Mesh.Should().Be("Foliage/Oak.fvfoliage#crown.1");
        world.Has<StaticBody>(crown).Should().BeFalse("leaves stop nobody");
    }

    [Fact]
    public void AScatterSpawnsItsCopiesWithinItsRadiusSharingTheFilesMeshes()
    {
        _builder.Write(FoliageFile.Create(FoliageKind.Bush));

        Entity patch = _builder.Place("foliageScatter", $$"""{ "file": "{{FoliageEntityTestsBuilder.File}}", "count": 5, "radius": 3 }""");

        World world = _builder.World;
        _builder.Parts.FindAll(patch).Should().HaveCount(15);
        Entity leaves = _builder.Parts.Find(patch, "4.crown.2");
        world.Get<MeshRenderer>(leaves).Mesh.Should().Be("Foliage/Oak.fvfoliage#crown.2");
        world.Has<StaticBody>(leaves).Should().BeFalse();
        Vector3 middle = world.Get<Transform>(_builder.Parts.Find(patch, "4.crown.1")).Position;
        new Vector2(middle.X, middle.Z).Length().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void AScatterPlacesItsCopiesTheSameWayEveryTimeAndAnotherWayForAnotherSeed()
    {
        FoliageScatter scatter = new() { File = FoliageEntityTestsBuilder.File, Count = 20, Radius = 5, Seed = 2 };
        FoliageScatter other = scatter with { Seed = 3 };

        IReadOnlyList<Transform> places = FoliagePlacement.Scatter(scatter);

        places.Should().HaveCount(20);
        places.Should().OnlyContain(place => place.Position.Length() <= 5 && place.Position.Y == 0);
        places.Should().OnlyContain(place => place.Scale.X >= 0.8f && place.Scale.X <= 1.2f);
        FoliagePlacement.Scatter(scatter).Select(place => place.Position).Should().Equal(places.Select(place => place.Position));
        FoliagePlacement.Scatter(other).Select(place => place.Position).Should().NotEqual(places.Select(place => place.Position));
    }

    [Fact]
    public void AScatterMakesNoMoreCopiesThanItsLimit()
    {
        FoliageScatter scatter = new() { File = FoliageEntityTestsBuilder.File, Count = 5000 };

        FoliagePlacement.Scatter(scatter).Should().HaveCount(FoliageScatter.MaxCount);
    }

    [Fact]
    public void RespawnsThePartsWhenTheFileChangesKeepingThoseThatStay()
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree);
        _builder.Write(tree);
        Entity oak = _builder.PlacePlant();
        Entity trunk = _builder.Parts.Find(oak, "trunk");

        tree.Shape.Limbs = 1;
        tree.Grow();
        _builder.Write(tree);

        _builder.Parts.FindAll(oak).Should().HaveCount(7);
        _builder.Parts.Find(oak, "trunk").Should().Be(trunk);
    }

    [Fact]
    public void APlantWhoseFileIsMissingSpawnsNothing()
    {
        Entity oak = _builder.PlacePlant();

        _builder.Parts.FindAll(oak).Should().BeEmpty();
    }
}
