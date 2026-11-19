using FlickerVoid.Engine.Editor.Materials;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

public sealed class MaterialBrowserTests : IDisposable
{
    private readonly MaterialBrowserTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ListsTheMaterialFilesInTheContentFolderByPath()
    {
        _builder.WithFile("Walls/Plaster.material.json").WithFile("Brick.material.json").WithFile("Room.scene.json");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<string> materials = browser.Find("");

        materials.Should().Equal("Brick.material.json", "Walls/Plaster.material.json");
    }

    [Fact]
    public void FindsMaterialsWhosePathHoldsTheSearchText()
    {
        _builder.WithFile("Walls/Plaster.material.json").WithFile("Brick.material.json");
        MaterialBrowser browser = _builder.Build();

        IReadOnlyList<string> materials = browser.Find("wall");

        materials.Should().Equal("Walls/Plaster.material.json");
    }

    [Fact]
    public void CreatesAnEmptyMaterialUnderAFreeName()
    {
        _builder.WithFile("New Material.material.json");
        MaterialBrowser browser = _builder.Build();

        string created = browser.Create("New Material");

        created.Should().Be("New Material 2.material.json");
        string text = File.ReadAllText(Path.Combine(_builder.Folder, created));
        text.Should().Contain("parameters");
    }
}
