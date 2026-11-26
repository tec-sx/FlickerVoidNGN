using FlickerVoid.Editor.Assets.Materials;

namespace FlickerVoid.Editor.Assets.Tests.Materials;

public sealed class MaterialBrowserTests : IDisposable
{
    private readonly MaterialBrowserTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ListsTheMaterialFilesInTheContentFolderByPath()
    {
        _builder.WithFile("Walls/Plaster.fvmat").WithFile("Brick.fvmat").WithFile("Room.fvscene");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<string> materials = browser.Find("");

        materials.Should().Equal("Brick.fvmat", "Walls/Plaster.fvmat");
    }

    [Fact]
    public void FindsMaterialsWhosePathHoldsTheSearchText()
    {
        _builder.WithFile("Walls/Plaster.fvmat").WithFile("Brick.fvmat");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<string> materials = browser.Find("wall");

        materials.Should().Equal("Walls/Plaster.fvmat");
    }

    [Fact]
    public void CreatesAnEmptyMaterialUnderAFreeName()
    {
        _builder.WithFile("New Material.fvmat");
        MaterialBrowser browser = _builder.Build();

        string created = browser.Create("New Material");

        created.Should().Be("New Material 2.fvmat");
        string text = File.ReadAllText(Path.Combine(_builder.Folder, created));
        text.Should().Contain("parameters");
    }

    [Fact]
    public void ArrangesTheMaterialsUnderTheMastersAndMaterialsTheyAreInstancesOf()
    {
        _builder
            .WithFile("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster" }""")
            .WithFile("Red Brick.fvmat", """{ "parent": "Brick.fvmat" }""")
            .WithFile("Concrete.fvmat", """{ "parent": "Masters/Surface.fvmaster" }""")
            .WithFile("Plain.fvmat");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<MaterialTreeNode> tree = browser.FindTree("");

        tree.Select(node => node.Path).Should().Equal("Masters/Surface.fvmaster", "Plain.fvmat");
        MaterialTreeNode master = tree[0];
        master.IsMaster.Should().BeTrue();
        master.Children.Select(node => node.Path).Should().Equal("Brick.fvmat", "Concrete.fvmat");
        master.Children[0].Children.Select(node => node.Name).Should().Equal("Red Brick");
    }

    [Fact]
    public void KeepsTheParentsOfAMatchInTheTree()
    {
        _builder
            .WithFile("Brick.fvmat", """{ "parent": "Masters/Surface.fvmaster" }""")
            .WithFile("Red Brick.fvmat", """{ "parent": "Brick.fvmat" }""")
            .WithFile("Concrete.fvmat", """{ "parent": "Masters/Surface.fvmaster" }""");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<MaterialTreeNode> tree = browser.FindTree("red");

        tree.Should().ContainSingle().Which.Children.Should().ContainSingle().Which.Children.Should().ContainSingle().Which.Path.Should().Be("Red Brick.fvmat");
    }

    [Fact]
    public void ShowsAMaterialWhoseFileCantBeReadAtTheTop()
    {
        _builder.WithFile("Broken.fvmat", "not json");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<MaterialTreeNode> tree = browser.FindTree("");

        tree.Should().ContainSingle().Which.Path.Should().Be("Broken.fvmat");
    }

    [Fact]
    public void CreatesAnInstanceThatKeepsItsParentsValues()
    {
        MaterialBrowser browser = _builder.Build();

        string path = browser.Create("Brick Instance", "Masters/Surface.fvmaster");

        path.Should().Be("Brick Instance.fvmat");
        string written = File.ReadAllText(Path.Combine(_builder.Folder, path));
        written.Should().Be("""{ "parent": "Masters/Surface.fvmaster", "parameters": {} }""");
    }
}
