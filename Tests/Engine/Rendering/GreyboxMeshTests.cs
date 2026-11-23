using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class GreyboxMeshTests
{
    [Fact]
    public void NamesReadBackTheSizeTheyName()
    {
        Vector3 size = new(4, 3, 0.2f);

        string name = GreyboxMesh.GetName(size);
        bool read = GreyboxMesh.TryGetSize(name, out Vector3 named);

        read.Should().BeTrue();
        named.Should().Be(size);
    }

    [Fact]
    public void ModelFilesAreNotGreyboxes()
    {
        bool read = GreyboxMesh.TryGetSize("Walls/Plain.glb", out _);

        read.Should().BeFalse();
    }

    [Fact]
    public void BoxesStandOnTheOriginCentredAcross()
    {
        ModelData model = GreyboxMesh.Create(new Vector3(4, 3, 2));

        MeshVertex[] vertices = model.Parts.Single().Vertices;
        Vector3 min = vertices.Select(vertex => vertex.Position).Aggregate(Vector3.Min);
        Vector3 max = vertices.Select(vertex => vertex.Position).Aggregate(Vector3.Max);
        min.Should().Be(new Vector3(-2, 0, -1));
        max.Should().Be(new Vector3(2, 3, 1));
    }

    [Fact]
    public void FacesTurnClockwiseSeenFromOutside()
    {
        ModelData model = GreyboxMesh.Create(new Vector3(4, 3, 2));

        MeshPart part = model.Parts.Single();
        part.Indices.Should().HaveCount(36);
        for (int index = 0; index < part.Indices.Length; index += 3)
        {
            MeshVertex a = part.Vertices[part.Indices[index]];
            Vector3 b = part.Vertices[part.Indices[index + 1]].Position;
            Vector3 c = part.Vertices[part.Indices[index + 2]].Position;
            Vector3 counterClockwise = Vector3.Cross(b - a.Position, c - a.Position);
            Vector3.Dot(counterClockwise, a.Normal).Should().BeNegative();
        }
    }

    [Fact]
    public void TextureCoordinatesMeasureMetres()
    {
        ModelData model = GreyboxMesh.Create(new Vector3(4, 3, 2));

        MeshVertex[] front = [.. model.Parts.Single().Vertices.Where(vertex => vertex.Normal == Vector3.UnitZ)];
        float width = front.Max(vertex => vertex.Uv.X) - front.Min(vertex => vertex.Uv.X);
        float height = front.Max(vertex => vertex.Uv.Y) - front.Min(vertex => vertex.Uv.Y);
        width.Should().Be(4);
        height.Should().Be(3);
    }
}
