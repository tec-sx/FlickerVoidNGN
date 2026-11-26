using System.Numerics;

using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshInsetTests
{
    private const int Top = 2;

    private readonly SolidMesh _box = MeshTestsBuilder.Box();

    [Fact]
    public void InsettingTheTopFacePullsItsBorderInAndRingsItWithFaces()
    {
        MeshResult? result = MeshInset.Faces(_box, new HashSet<int> { Top }, 0.2f);

        result.Should().NotBeNull();
        result!.Solid.Points.Should().HaveCount(12);
        result.Solid.Faces.Should().HaveCount(10);
        IReadOnlyList<int> inner = result.Solid.Faces[result.Faces[0]];
        foreach (int point in inner)
        {
            Vector3 position = result.Solid.Points[point];
            position.Y.Should().BeApproximately(1, 1e-5f);
            MathF.Abs(position.X).Should().BeApproximately(0.3f, 1e-5f);
            MathF.Abs(position.Z).Should().BeApproximately(0.3f, 1e-5f);
        }
    }

    [Fact]
    public void ThePointsInsideARegionStayWhereTheyAre()
    {
        SolidMesh bar = MeshTestsBuilder.Bar(3);
        HashSet<int> sides = [0, 1, 2, 3];

        MeshResult? result = MeshInset.Faces(bar, sides, 0.1f);

        result.Should().NotBeNull();
        result!.Solid.Points.Take(12).Should().Equal(bar.Points);
    }

    [Fact]
    public void NothingOrEverythingHasNoBorderToInset()
    {
        MeshInset.Faces(_box, new HashSet<int>(), 0.1f).Should().BeNull();
        MeshInset.Faces(_box, new HashSet<int>(Enumerable.Range(0, 6)), 0.1f).Should().BeNull();
    }

    [Fact]
    public void AZeroThicknessKeepsTheShapeAndStillMakesTheRing()
    {
        MeshResult? result = MeshInset.Faces(_box, new HashSet<int> { Top }, 0);

        result!.Solid.Faces.Should().HaveCount(10);
        result.Solid.Points.Max(point => point.Y).Should().BeApproximately(1, 1e-5f);
    }
}
