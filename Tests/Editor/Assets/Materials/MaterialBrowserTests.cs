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
}
