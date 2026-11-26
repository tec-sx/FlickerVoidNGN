using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Tests.Materials;

public sealed class MaterialChainTests : IDisposable
{
    private readonly MaterialChainTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void KeepsAMaterialWithoutAParentAsItIs()
    {
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parameters": { "wrap": 0.5 } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parameters.Keys.Should().Equal("wrap");
        _builder.Loaded.Should().BeEmpty();
    }

    [Fact]
    public void InheritsTheValuesItLeavesOutFromItsParent()
    {
        _builder.With("Brick.fvmat", """{ "parameters": { "wrap": 0.5, "specular": 1 } }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat", "parameters": { "tint": [1, 0, 0] } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parameters.Keys.Should().BeEquivalentTo("wrap", "specular", "tint");
        flat.Parent.Should().BeNull();
    }

    [Fact]
    public void KeepsItsOwnValuesOverItsParents()
    {
        _builder.With("Brick.fvmat", """{ "parameters": { "wrap": 0.5 } }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat", "parameters": { "wrap": 0.25 } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parameters["wrap"].GetSingle().Should().Be(0.25f);
    }

    [Fact]
    public void InheritsUpTheWholeChain()
    {
        _builder
            .With("Surface.fvmat", """{ "parameters": { "wrap": 0.5, "specular": 1 } }""")
            .With("Brick.fvmat", """{ "parent": "Surface.fvmat", "parameters": { "wrap": 0.25 } }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat", "parameters": { "tint": [1, 0, 0] } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parameters["wrap"].GetSingle().Should().Be(0.25f);
        flat.Parameters["specular"].GetSingle().Should().Be(1);
        _builder.Loaded.Should().Equal("Brick.fvmat", "Surface.fvmat");
    }

    [Fact]
    public void RefusesAChainThatComesBackToItself()
    {
        _builder
            .With("Brick.fvmat", """{ "parent": "Wall.fvmat" }""")
            .With("Wall.fvmat", """{ "parent": "Brick.fvmat" }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat" }""");

        Action flatten = () => MaterialChain.Flatten(material, _builder.Load);

        flatten.Should().Throw<InvalidDataException>().WithMessage("*Brick.fvmat*Wall.fvmat*Brick.fvmat*");
    }

    [Fact]
    public void ClearsAParentsValueWithNull()
    {
        _builder.With("Brick.fvmat", """{ "parameters": { "texture": "Paintings/Brick.reb" } }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat", "parameters": { "texture": null } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parameters["texture"].ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void IsCheckedForAParentTheContentLacks()
    {
        _builder.With("RedBrick.fvmat", """{ "parent": "Brick.fvmat" }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle().Which.Message.Should().Contain("Brick.fvmat");
    }

    [Fact]
    public void IsCheckedForAParentThatIsNoMaterial()
    {
        _builder
            .With("Brick.json", "{}")
            .With("RedBrick.fvmat", """{ "parent": "Brick.json" }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle().Which.Message.Should().Contain("isn't a .fvmat or .fvmaster file");
    }

    [Fact]
    public void EndsTheChainAtItsMaster()
    {
        _builder
            .With("Surface.fvmat", """{ "parent": "Masters/Brick.fvmaster", "parameters": { "wrap": 0.5 } }""")
            .With("Brick.fvmat", """{ "parent": "Surface.fvmat" }""");
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Brick.fvmat", "parameters": { "mortar": 1 } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Parent.Should().Be("Masters/Brick.fvmaster");
        flat.Parameters.Keys.Should().BeEquivalentTo("wrap", "mortar");
        _builder.Loaded.Should().Equal("Brick.fvmat", "Surface.fvmat");
    }

    [Fact]
    public void KeepsAnInstanceOfAMasterAsItIs()
    {
        MaterialFile material = MaterialChainTestsBuilder.Material("""{ "parent": "Masters/Brick.fvmaster", "parameters": { "mortar": 1 } }""");

        MaterialFile flat = MaterialChain.Flatten(material, _builder.Load);

        flat.Should().BeSameAs(material);
        _builder.Loaded.Should().BeEmpty();
    }

    [Fact]
    public void IsCheckedWithTheEnginesMastersAsItsContent()
    {
        _builder.With("Wall.fvmat", """{ "parent": "Engine/Rendering/Masters/Painted.fvmaster" }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().BeEmpty();
    }

    [Fact]
    public void IsCheckedForAnEngineMasterThatIsNotBuilt()
    {
        _builder.With("Wall.fvmat", """{ "parent": "Engine/Rendering/Masters/Missing.fvmaster" }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().ContainSingle().Which.Message.Should().Contain("Missing.fvmaster");
    }
}
