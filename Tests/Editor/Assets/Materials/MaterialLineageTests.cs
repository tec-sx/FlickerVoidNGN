using System.Numerics;
using System.Text.Json;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Editor.Assets.Tests.Materials;

public sealed class MaterialLineageTests : IDisposable
{
    private const string Master = """
        { "shadingModel": "Painted", "parameters": [{ "name": "mortar", "type": "float", "default": 0.25 }] }
        """;

    private static readonly ShaderParameter Wrap = new("wrap", ShaderParameterType.Float, JsonDocument.Parse("0").RootElement);

    private static readonly ShaderParameter Tint = new("tint", ShaderParameterType.Color, JsonDocument.Parse("[1, 1, 1]").RootElement);

    private readonly MaterialLineageTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void InheritsEachValueFromTheNearestParentSettingIt()
    {
        _builder
            .WithFile("Surface.fvmat", """{ "parameters": { "wrap": 0.5, "tint": [1, 0, 0] } }""")
            .WithFile("Brick.fvmat", """{ "parent": "Surface.fvmat", "parameters": { "wrap": 0.25 } }""");
        MaterialFile material = MaterialLineageTestsBuilder.Material("""{ "parent": "Brick.fvmat" }""");

        MaterialLineage lineage = MaterialLineage.Of(material, _builder.Content);

        lineage.Inherited["wrap"].Source.Should().Be("Brick.fvmat");
        lineage.Inherited["wrap"].Value.GetSingle().Should().Be(0.25f);
        lineage.Inherited["tint"].Source.Should().Be("Surface.fvmat");
        lineage.Master.Should().BeNull();
    }

    [Fact]
    public void AddsTheMastersParametersAfterTheSceneShaders()
    {
        _builder
            .WithFile("Masters/Brick.fvmaster", Master)
            .WithFile("Brick.fvmat", """{ "parent": "Masters/Brick.fvmaster" }""");
        MaterialFile material = MaterialLineageTestsBuilder.Material("""{ "parent": "Brick.fvmat" }""");

        MaterialLineage lineage = MaterialLineage.Of(material, _builder.Content);

        lineage.MasterPath.Should().Be("Masters/Brick.fvmaster");
        IReadOnlyList<ShaderParameter> parameters = lineage.GetParameters([Wrap]);
        parameters.Select(parameter => parameter.Name).Should().Equal("wrap", "mortar");
    }

    [Fact]
    public void RefusesParentsThatComeBackToThemselves()
    {
        _builder
            .WithFile("A.fvmat", """{ "parent": "B.fvmat" }""")
            .WithFile("B.fvmat", """{ "parent": "A.fvmat" }""");
        MaterialFile material = MaterialLineageTestsBuilder.Material("""{ "parent": "A.fvmat" }""");

        Action trace = () => MaterialLineage.Of(material, _builder.Content);

        trace.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ShowsTheParentsValueUntilTheMaterialOverridesIt()
    {
        _builder
            .WithFile("Brick.fvmat", """{ "parameters": { "tint": [1, 0, 0] } }""")
            .WithFile("Red Brick.fvmat", """{ "parent": "Brick.fvmat", "parameters": {} }""");
        MaterialDocument document = _builder.Open("Red Brick.fvmat");
        MaterialLineage lineage = MaterialLineage.Of(document.Material, _builder.Content);
        MaterialParameterValue tint = new(document, Tint, lineage.Inherited.GetValueOrDefault("tint"));

        tint.Get().Should().Be(new Vector3(1, 0, 0));
        tint.IsOverridden.Should().BeFalse();
        tint.Source.Should().Be("Brick.fvmat");

        tint.Set(new Vector3(0, 0, 1));

        tint.IsOverridden.Should().BeTrue();
        tint.Get().Should().Be(new Vector3(0, 0, 1));
    }

    [Fact]
    public void ResetsAnOverriddenValueToTheParents()
    {
        _builder
            .WithFile("Brick.fvmat", """{ "parameters": { "wrap": 0.5 } }""")
            .WithFile("Red Brick.fvmat", """{ "parent": "Brick.fvmat", "parameters": { "wrap": 0.25 } }""");
        MaterialDocument document = _builder.Open("Red Brick.fvmat");
        MaterialLineage lineage = MaterialLineage.Of(document.Material, _builder.Content);
        MaterialParameterValue wrap = new(document, Wrap, lineage.Inherited.GetValueOrDefault("wrap"));

        wrap.Reset();

        wrap.IsOverridden.Should().BeFalse();
        wrap.Get().Should().Be(0.5f);
        document.Material.Parameters.Should().NotContainKey("wrap");
    }

    [Fact]
    public void OverridesWithTheValueShown()
    {
        _builder
            .WithFile("Brick.fvmat", """{ "parameters": { "wrap": 0.5 } }""")
            .WithFile("Red Brick.fvmat", """{ "parent": "Brick.fvmat", "parameters": {} }""");
        MaterialDocument document = _builder.Open("Red Brick.fvmat");
        MaterialLineage lineage = MaterialLineage.Of(document.Material, _builder.Content);
        MaterialParameterValue wrap = new(document, Wrap, lineage.Inherited.GetValueOrDefault("wrap"));

        wrap.Override();

        document.Material.Parameters["wrap"].GetSingle().Should().Be(0.5f);
    }

    [Fact]
    public void ChangesTheParentOfTheMaterial()
    {
        _builder.WithFile("Red Brick.fvmat", """{ "parameters": {} }""");
        MaterialDocument document = _builder.Open("Red Brick.fvmat");
        MaterialParentValue parent = new(document);

        parent.Set("Masters/Brick.fvmaster");

        document.Material.Parent.Should().Be("Masters/Brick.fvmaster");
        _builder.Load("Red Brick.fvmat").Parent.Should().Be("Masters/Brick.fvmaster", "the edit is served unsaved");
    }
}
