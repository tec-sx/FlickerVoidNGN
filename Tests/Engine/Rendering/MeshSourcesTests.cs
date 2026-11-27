using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Greyboxes;
using FlickerVoid.Engine.Runtime.Solids;

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
    public void MakesAShapedGreyboxFromItsName()
    {
        MeshSources sources = _builder.Build();
        SolidMesh box = SolidMesh.Box(new Vector3(4, 3, 2));
        SolidMesh wedge = box.Transform(Matrix4x4.CreateScale(1, 2, 1));
        Greybox greybox = new() { Size = new Vector3(4, 3, 2), Mesh = wedge };
        string name = GreyboxMesh.GetName(greybox);

        MadeMesh? made = sources.Make(name, _builder.Content);

        made.Should().NotBeNull();
        MeshVertex[] vertices = made!.Model.Parts.Single().Vertices;
        vertices.Select(vertex => vertex.Position).Aggregate(Vector3.Max).Should().Be(new Vector3(2, 6, 1), "it is drawn as it was shaped, not as its size");
        GreyboxMesh.GetName(greybox).Should().BeSameAs(name, "a shape's name is made once");
    }

    [Fact]
    public void NamesAGreyboxByItsSwapThenItsShapeThenItsSize()
    {
        SolidMesh shape = SolidMesh.Box(new Vector3(1, 1, 1));
        Greybox plain = new() { Size = new Vector3(4, 3, 2) };
        Greybox shaped = new() { Size = new Vector3(4, 3, 2), Mesh = shape };
        Greybox swapped = new() { Size = new Vector3(4, 3, 2), Mesh = shape, Swaps = ["Walls/Plain.glb"], Swap = 1 };

        GreyboxMesh.GetName(plain).Should().Be(GreyboxMesh.GetName(new Vector3(4, 3, 2)));
        GreyboxMesh.GetName(shaped).Should().Be(GreyboxMesh.GetName(shape));
        GreyboxMesh.GetName(swapped).Should().Be("Walls/Plain.glb");
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
