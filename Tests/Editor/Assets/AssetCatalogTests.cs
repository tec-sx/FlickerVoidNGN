namespace FlickerVoid.Editor.Assets.Tests;

public sealed class AssetCatalogTests : IDisposable
{
    private readonly AssetCatalogTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData("Scenes/Room.fvscene", "Scene")]
    [InlineData("Room.glb", "Model")]
    [InlineData("Paint/Wall.fvmat", "Material")]
    [InlineData("Paper.png", "Texture")]
    [InlineData("Wall.reb", "Texture")]
    [InlineData("Mesh.hlsl", "Shader")]
    [InlineData("Mesh.fvshader", "Shader")]
    [InlineData("Moods/Night.fvmood", "Mood")]
    [InlineData("Canvases/Paper.fvcanvas", "Canvas")]
    [InlineData("Vans/Van.fvvehicle", "Vehicle")]
    [InlineData("Notes.txt", "Other")]
    public void TellsTheTypeOfAnAssetFromItsName(string path, string type)
    {
        AssetCatalog catalog = _builder.WithFile(path).Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, type: null, text: "");

        assets.Should().ContainSingle().Which.Type.Name.Should().Be(type);
    }

    [Fact]
    public void ListsAnImagesSettingsAsPartOfTheImage()
    {
        AssetCatalog catalog = _builder.WithFile("Paper.png").WithFile("Paper.png.fvtexture").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, type: null, text: "");

        assets.Should().ContainSingle().Which.Path.Should().Be("Paper.png");
    }

    [Fact]
    public void ListsAFoldersOwnAssetsSortedByName()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/b.fvscene").WithFile("Scenes/A.fvscene").WithFile("Scenes/Old/C.fvscene").WithFile("Room.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("Scenes", recursive: false, type: null, text: "");

        assets.Select(static asset => asset.Path).Should().Equal("Scenes/A.fvscene", "Scenes/b.fvscene");
        assets[0].Name.Should().Be("A");
    }

    [Fact]
    public void FiltersByKindAndTextBelowTheFolder()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/Room.fvscene").WithFile("Scenes/Old/Hall.fvscene").WithFile("Hall.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, AssetTypes.Scene, text: "hall");

        assets.Select(static asset => asset.Path).Should().Equal("Scenes/Old/Hall.fvscene");
    }

    [Fact]
    public void ListsFoldersAsATreeWithoutHiddenOnes()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/Old/C.fvscene").WithFile("Moods/Day.fvmood").WithFile(".schemas/scene.schema.json").Build();

        AssetFolder root = catalog.GetFolders();

        root.Children.Select(static folder => folder.Name).Should().Equal("Moods", "Scenes");
        root.Children[1].Children.Should().ContainSingle().Which.Path.Should().Be("Scenes/Old");
    }

    [Fact]
    public void SkipsHiddenFiles()
    {
        AssetCatalog catalog = _builder.WithFile(".build-cache.json").WithFile(".schemas/scene.schema.json").WithFile("Room.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, type: null, text: "");

        assets.Select(static asset => asset.Path).Should().Equal("Room.glb");
    }
}
