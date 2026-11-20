using FlickerVoid.Engine.Editor.Assets;

namespace FlickerVoid.Engine.Editor.Tests.Assets;

public sealed class AssetCatalogTests : IDisposable
{
    private readonly AssetCatalogTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData("Scenes/Room.fvscene", AssetKind.Scene)]
    [InlineData("Room.glb", AssetKind.Model)]
    [InlineData("Paint/Wall.fvmat", AssetKind.Material)]
    [InlineData("Paper.png", AssetKind.Texture)]
    [InlineData("Mesh.hlsl", AssetKind.Shader)]
    [InlineData("Mesh.fvshader", AssetKind.Shader)]
    [InlineData("Moods/Night.fvmood", AssetKind.Mood)]
    [InlineData("Notes.txt", AssetKind.Other)]
    public void TellsTheKindOfAnAssetFromItsName(string path, AssetKind kind)
    {
        AssetCatalog catalog = _builder.WithFile(path).Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, kind: null, text: "");

        assets.Should().ContainSingle().Which.Kind.Should().Be(kind);
    }

    [Fact]
    public void ListsAFoldersOwnAssetsSortedByName()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/b.fvscene").WithFile("Scenes/A.fvscene").WithFile("Scenes/Old/C.fvscene").WithFile("Room.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("Scenes", recursive: false, kind: null, text: "");

        assets.Select(static asset => asset.Path).Should().Equal("Scenes/A.fvscene", "Scenes/b.fvscene");
        assets[0].Name.Should().Be("A");
    }

    [Fact]
    public void FiltersByKindAndTextBelowTheFolder()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/Room.fvscene").WithFile("Scenes/Old/Hall.fvscene").WithFile("Hall.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, AssetKind.Scene, text: "hall");

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

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, kind: null, text: "");

        assets.Select(static asset => asset.Path).Should().Equal("Room.glb");
    }
}
