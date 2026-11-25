using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class MeshSourcesTests : IDisposable
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly MeshSourcesTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void MakesGreyboxBoxesFromTheirNames()
    {
        MeshSources sources = _builder.Build();
        string name = GreyboxMesh.GetName(new Vector3(4, 3, 2));

        MadeMesh? made = sources.Make(name, _builder.Content);

        made.Should().NotBeNull();
        made!.File.Should().BeNull("a box is made from its name alone");
        MeshVertex[] vertices = made.Model.Parts.Single().Vertices;
        vertices.Select(vertex => vertex.Position).Aggregate(Vector3.Max).Should().Be(new Vector3(2, 3, 1));
    }

    [Fact]
    public void LoadsTheModelTheContentBuildBaked()
    {
        _builder.WithModel("Walls/Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        MeshSources sources = _builder.Build();

        MadeMesh? made = sources.Make("Walls/Quad.glb", _builder.Content);

        made.Should().NotBeNull();
        made!.File.Should().Be("Walls/Quad.fvmodel");
        made.Model.Parts.Single().Material.BaseColor.Should().Be(Red);
    }

    [Fact]
    public void MakesNothingOfAMissingModel()
    {
        MeshSources sources = _builder.Build();

        MadeMesh? made = sources.Make("Missing.glb", _builder.Content);

        made.Should().BeNull();
    }

    [Fact]
    public void AsksTheSourcesAddedBeforeTheModelFiles()
    {
        _builder.WithFile("Parts/Van.fvtest", "2");
        MeshSources sources = _builder.WithSource(new TestMeshSource()).Build();

        MadeMesh? made = sources.Make("Parts/Van.fvtest#body", _builder.Content);

        made.Should().NotBeNull();
        made!.File.Should().Be("Parts/Van.fvtest");
        made.Model.Parts.Single().Material.BaseColor.Should().Be(SolidModel.Color);
    }

    [Fact]
    public void LeavesWhatNoSourceMakesToTheModelFiles()
    {
        _builder.WithModel("Quad.glb", static model => model.WithQuad(1).WithBaseColor(Red));
        MeshSources sources = _builder.WithSource(new TestMeshSource()).Build();

        MadeMesh? made = sources.Make("Quad.glb", _builder.Content);

        made!.File.Should().Be("Quad.fvmodel");
    }
}
