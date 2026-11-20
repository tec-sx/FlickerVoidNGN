using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Tools.Builder.Tests.Models;

public sealed class ModelImporterTests : IDisposable
{
    private readonly ModelImporterTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void BakesAModelIntoAFileTheGameReads()
    {
        Vector4 orange = new(1, 0.5f, 0, 1);
        _files.WithGlb("Props/Crate.glb", model => model.WithQuad(1).WithBaseColor(orange).WithMovingJoint(Vector3.Zero, Vector3.UnitX, 1));
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().BeEmpty();
        ModelData model = _files.ReadOutput("Props/Crate.fvmodel");
        MeshPart part = model.Parts.Should().ContainSingle().Subject;
        part.Indices.Should().HaveCount(6);
        part.Skin.Should().HaveCount(part.Vertices.Length);
        part.Material.BaseColor.Should().Be(orange);
        model.Animations.Should().ContainSingle().Which.Name.Should().Be("Move");
    }

    [Fact]
    public void RebuildsAGltfWhenAnImageItNamesChanges()
    {
        _files.WithGltf("Quad.gltf", static model => model.WithQuad(1).WithRedBlueTexture());
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.OverwriteImages(GltfImporterTestsBuilder.BlueRedPng);

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Quad.gltf");
    }
}
