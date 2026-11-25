using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Tests.Solids;

public sealed class SolidMeshTests
{
    private const float Precision = 1e-4f;

    private static readonly Vector3[] CubePoints =
    [
        new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1),
    ];

    /// <summary>A unit cube's faces, each counter-clockwise seen from outside.</summary>
    private static readonly int[][] CubeFaces =
    [
        [0, 3, 2, 1], [4, 5, 6, 7], [0, 4, 7, 3], [1, 2, 6, 5], [0, 1, 5, 4], [3, 7, 6, 2],
    ];

    [Fact]
    public void AClosedSolidKeepsItsPointsAndFaces()
    {
        SolidMesh cube = new(CubePoints, CubeFaces);

        cube.Points.Should().Equal(CubePoints);
        cube.Faces.Should().HaveCount(6);
        cube.Faces[1].Should().Equal(4, 5, 6, 7);
    }

    [Fact]
    public void RefusesAnOpenSolidNamingTheOpenEdge()
    {
        int[][] withoutTop = CubeFaces[..5];

        Action create = () => _ = new SolidMesh(CubePoints, withoutTop);

        create.Should().Throw<ArgumentException>().WithMessage("*open*edge from point*");
    }

    [Fact]
    public void RefusesAFaceTurnedTheWrongWay()
    {
        int[][] faces = [.. CubeFaces];
        faces[1] = [7, 6, 5, 4];

        Action create = () => _ = new SolidMesh(CubePoints, faces);

        create.Should().Throw<ArgumentException>().WithMessage("*wrong way*");
    }

    [Fact]
    public void RefusesFacesOfFewerThanThreePointsOrOfPointsItDoesNotHave()
    {
        int[][] tooSmall = [.. CubeFaces, [0, 1]];
        int[][] missingPoint = [.. CubeFaces[..5], [3, 7, 6, 8]];

        Action small = () => _ = new SolidMesh(CubePoints, tooSmall);
        Action missing = () => _ = new SolidMesh(CubePoints, missingPoint);

        small.Should().Throw<ArgumentException>().WithMessage("*face 6*three points*");
        missing.Should().Throw<ArgumentException>().WithMessage("*face 5*point 8*");
    }

    [Fact]
    public void ABoxStandsOnItsOriginAndIsClosed()
    {
        SolidMesh box = SolidMesh.Box(new Vector3(2, 1, 3));

        box.Points.Should().HaveCount(8);
        box.Faces.Should().HaveCount(6);
        Vector3 lowest = box.Points.Aggregate(Vector3.Min);
        Vector3 highest = box.Points.Aggregate(Vector3.Max);
        lowest.Should().Be(new Vector3(-1, 0, -1.5f));
        highest.Should().Be(new Vector3(1, 1, 1.5f));
    }

    [Fact]
    public void ABoxMakesTwelveTrianglesWithFlatNormalsAndTextureCoordinatesInMetres()
    {
        SolidMesh box = SolidMesh.Box(new Vector3(2, 1, 3));

        SolidTriangles triangles = box.Triangulate();

        triangles.Indices.Should().HaveCount(36);
        triangles.Vertices.Should().HaveCount(24);
        SolidVertex[] front = [.. triangles.Vertices.Where(vertex => vertex.Normal == Vector3.UnitZ)];
        SolidVertex[] top = [.. triangles.Vertices.Where(vertex => vertex.Normal == Vector3.UnitY)];
        front.Should().HaveCount(4);
        top.Should().HaveCount(4);
        SolidVertex frontTopLeft = front.Single(vertex => vertex.Position == new Vector3(-1, 1, 1.5f));
        SolidVertex frontBottomRight = front.Single(vertex => vertex.Position == new Vector3(1, 0, 1.5f));
        frontTopLeft.TextureCoordinates.Should().Be(Vector2.Zero);
        frontBottomRight.TextureCoordinates.Should().Be(new Vector2(2, 1));
        Vector2 topSpan = top.Select(vertex => vertex.TextureCoordinates).Aggregate(Vector2.Max);
        topSpan.Should().Be(new Vector2(2, 3));
    }

    [Fact]
    public void TrianglesFaceOutwardAndAreWoundClockwiseSeenFromOutsideAsTheEngineDraws()
    {
        SolidMesh box = SolidMesh.Box(new Vector3(2, 1, 3));
        SolidMesh cylinder = SolidMesh.Cylinder(0.4f, 0.3f, 12);

        SolidTriangles boxTriangles = box.Triangulate();
        SolidTriangles cylinderTriangles = cylinder.Triangulate();

        AllClockwise(boxTriangles).Should().BeTrue();
        AllClockwise(cylinderTriangles).Should().BeTrue();
        AllOutward(boxTriangles, new Vector3(0, 0.5f, 0)).Should().BeTrue();
        AllOutward(cylinderTriangles, new Vector3(0, 0.15f, 0)).Should().BeTrue();
    }

    [Fact]
    public void ACylinderStandsOnItsOriginWithFlatSidesAndIsClosed()
    {
        SolidMesh cylinder = SolidMesh.Cylinder(0.4f, 0.3f, 12);

        SolidTriangles triangles = cylinder.Triangulate();

        cylinder.Points.Should().HaveCount(24);
        cylinder.Faces.Should().HaveCount(14);
        cylinder.Points.Should().OnlyContain(point => MathF.Abs(new Vector2(point.X, point.Z).Length() - 0.4f) < Precision);
        cylinder.Points.Select(point => point.Y).Distinct().Should().BeEquivalentTo([0f, 0.3f]);
        triangles.Indices.Should().HaveCount(3 * ((12 * 2) + (2 * 10)));
        triangles.Vertices.Where(vertex => vertex.Normal.Y == 0).Should().OnlyContain(vertex => MathF.Abs(vertex.Normal.Length() - 1) < Precision);
    }

    [Fact]
    public void RefusesACylinderOfFewerThanThreeSides()
    {
        Action create = () => SolidMesh.Cylinder(0.4f, 0.3f, 2);

        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AMirroredSolidStaysClosedAndFacesOutward()
    {
        SolidMesh box = SolidMesh.Box(new Vector3(2, 1, 3));
        Matrix4x4 mirrorAndMove = Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateTranslation(5, 0, 0);

        SolidMesh mirrored = box.Transform(mirrorAndMove);
        SolidTriangles triangles = mirrored.Triangulate();

        mirrored.Points.Aggregate(Vector3.Min).Should().Be(new Vector3(4, 0, -1.5f));
        AllClockwise(triangles).Should().BeTrue();
        AllOutward(triangles, new Vector3(5, 0.5f, 0)).Should().BeTrue();
        SolidVertex[] right = [.. triangles.Vertices.Where(vertex => vertex.Normal == Vector3.UnitX)];
        right.Should().HaveCount(4).And.OnlyContain(vertex => vertex.Position.X == 6);
    }

    /// <summary>Whether every vertex's normal points away from the centre of a convex solid.</summary>
    private static bool AllOutward(SolidTriangles triangles, Vector3 centre)
    {
        return triangles.Vertices.All(vertex => Vector3.Dot(vertex.Normal, vertex.Position - centre) > 0);
    }

    /// <summary>Whether every triangle turns clockwise seen from the side its normal points to.</summary>
    private static bool AllClockwise(SolidTriangles triangles)
    {
        for (int i = 0; i < triangles.Indices.Length; i += 3)
        {
            SolidVertex a = triangles.Vertices[triangles.Indices[i]];
            SolidVertex b = triangles.Vertices[triangles.Indices[i + 1]];
            SolidVertex c = triangles.Vertices[triangles.Indices[i + 2]];
            Vector3 turn = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            if (Vector3.Dot(turn, a.Normal) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
