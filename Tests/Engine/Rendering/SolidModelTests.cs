using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class SolidModelTests
{
    [Fact]
    public void DrawsTheSolidsTrianglesAsOneGreyPart()
    {
        SolidMesh solid = SolidMesh.Cylinder(radius: 1, height: 2, sides: 6);
        SolidTriangles triangles = solid.Triangulate();

        ModelData model = SolidModel.Create(solid);

        MeshPart part = model.Parts.Should().ContainSingle().Subject;
        part.Material.BaseColor.Should().Be(SolidModel.Color);
        part.Material.BaseColorTexture.Should().BeNull();
        part.Skin.Should().BeNull();
        part.Indices.Should().Equal(triangles.Indices);
        part.Vertices.Select(vertex => vertex.Position).Should().Equal(triangles.Vertices.Select(vertex => vertex.Position));
        part.Vertices.Select(vertex => vertex.Normal).Should().Equal(triangles.Vertices.Select(vertex => vertex.Normal));
        part.Vertices.Select(vertex => vertex.Uv).Should().Equal(triangles.Vertices.Select(vertex => vertex.TextureCoordinates));
        model.Animations.Should().BeEmpty();
    }
}
