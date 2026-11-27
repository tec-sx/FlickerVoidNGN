using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Foliage.Tests;

public sealed class FoliageFileTests
{
    [Fact]
    public void ANewTreeIsATrunkWithLimbsUnderACrown()
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree);

        tree.GetParts().Select(part => part.Id).Should().Equal(
            "trunk", "limb.1", "limb.2", "limb.3", "limb.4", "crown.1", "crown.2", "crown.3", "crown.4", "crown.5");
        tree.Trunk!.Mesh.Points.Max(point => point.Y).Should().BeApproximately(4.2f, 1e-4f, "the trunk ends where the crown begins");
        tree.Crowns[0].Place.Position.Should().Be(new Vector3(0, 4.2f, 0));
    }

    [Fact]
    public void ATreeStandsWithinItsHeight()
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree, seed: 7);

        foreach (SolidPart crown in tree.Crowns)
        {
            float top = crown.Place.Position.Y + crown.Mesh.Points.Max(point => point.Y);
            top.Should().BeLessThanOrEqualTo(tree.Shape.Height + 1e-3f);
        }
    }

    [Fact]
    public void ANewBushIsLeavesOnTheGroundWithoutATrunk()
    {
        FoliageFile bush = FoliageFile.Create(FoliageKind.Bush);

        bush.GetParts().Select(part => part.Id).Should().Equal("crown.1", "crown.2", "crown.3");
        bush.Trunk.Should().BeNull();
        float top = bush.Crowns.Max(crown => crown.Place.Position.Y + crown.Mesh.Points.Max(point => point.Y));
        top.Should().BeLessThanOrEqualTo(1 + 1e-3f);
    }

    [Fact]
    public void TheSameSeedGrowsTheSamePlantAndAnotherSeedAnother()
    {
        FoliageFile first = FoliageFile.Create(FoliageKind.Tree, seed: 3);
        FoliageFile same = FoliageFile.Create(FoliageKind.Tree, seed: 3);
        FoliageFile other = FoliageFile.Create(FoliageKind.Tree, seed: 4);

        same.Limbs.Select(limb => limb.Place.Position).Should().Equal(first.Limbs.Select(limb => limb.Place.Position));
        same.Crowns[1].Mesh.Points.Should().Equal(first.Crowns[1].Mesh.Points);
        other.Limbs.Select(limb => limb.Place.Position).Should().NotEqual(first.Limbs.Select(limb => limb.Place.Position));
    }

    [Fact]
    public void GrowingAgainFollowsTheShapeAndReplacesEveryPart()
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree);
        SolidPart trunk = tree.Trunk!;
        tree.Shape.Limbs = 2;
        tree.Shape.Crowns = 1;
        tree.Shape.Height = 10;

        tree.Grow();

        tree.GetParts().Select(part => part.Id).Should().Equal("trunk", "limb.1", "limb.2", "crown.1");
        tree.Trunk.Should().NotBeSameAs(trunk);
        tree.Trunk!.Mesh.Points.Max(point => point.Y).Should().BeApproximately(8.2f, 1e-4f);
    }

    [Fact]
    public void SetKindTurnsATreeIntoABushOfTheBushsShape()
    {
        FoliageFile foliage = FoliageFile.Create(FoliageKind.Tree);

        foliage.SetKind(FoliageKind.Bush);

        foliage.Trunk.Should().BeNull();
        foliage.Limbs.Should().BeEmpty();
        foliage.Shape.Height.Should().Be(1);
    }

    [Theory]
    [InlineData(0.1f, 5)]
    [InlineData(41, 5)]
    [InlineData(6, 0)]
    [InlineData(6, 17)]
    public void RefusesAShapeNothingGrowsFrom(float height, int crowns)
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree);
        tree.Shape.Height = height;
        tree.Shape.Crowns = crowns;

        Action act = tree.Grow;

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AFileReadsBackAsItWasWritten()
    {
        FoliageFile tree = FoliageFile.Create(FoliageKind.Tree, seed: 9);

        string json = FoliageFileTestsBuilder.Write(tree);
        FoliageFile read = FoliageFileTestsBuilder.Read(json);

        json.Should().NotContain("doors");
        read.Kind.Should().Be(FoliageKind.Tree);
        read.Seed.Should().Be(9);
        read.Shape.CrownRadius.Should().Be(1.8f);
        read.GetParts().Select(part => part.Id).Should().Equal(tree.GetParts().Select(part => part.Id));
        read.Limbs[0].Mesh.Points.Should().Equal(tree.Limbs[0].Mesh.Points);
        read.Limbs[0].Place.Rotation.Should().Be(tree.Limbs[0].Place.Rotation);
    }

    [Fact]
    public void ATrunkTapersAndABlobReachesItsRadii()
    {
        SolidMesh trunk = FoliageShapes.CreateTaper(0.4f, 0.2f, 3, 6);
        SolidMesh blob = FoliageShapes.CreateBlob(new Vector3(2, 1, 1.5f));

        trunk.Points.Should().HaveCount(12);
        trunk.Points.Where(point => point.Y == 0).Max(point => point.X).Should().BeApproximately(0.4f, 1e-4f);
        trunk.Points.Where(point => point.Y == 3).Max(point => point.X).Should().BeApproximately(0.2f, 1e-4f);
        blob.Points.Should().HaveCount(12);
        blob.Points.Max(point => point.X).Should().BeLessThanOrEqualTo(2).And.BeGreaterThan(1.6f);
        SolidTriangles triangles = blob.Triangulate();
        triangles.Vertices.Should().OnlyContain(vertex => Vector3.Dot(vertex.Normal, vertex.Position) > 0, "every face looks outward");
        trunk.Triangulate().Vertices.Should().Contain(vertex => vertex.Position.Y == 3 && vertex.Normal.Y > 0.9f, "the top looks up");
    }
}
