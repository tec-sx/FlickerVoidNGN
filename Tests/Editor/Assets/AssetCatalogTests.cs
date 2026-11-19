using FlickerVoid.Engine.Editor.Assets;

namespace FlickerVoid.Engine.Editor.Tests.Assets;

public sealed class AssetCatalogTests : IDisposable
{
    private readonly AssetCatalogTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Theory]
    [InlineData("Scenes/Room.scene.json", AssetKind.Scene)]
    [InlineData("Room.glb", AssetKind.Model)]
    [InlineData("Paint/Wall.material.json", AssetKind.Material)]
    [InlineData("Paper.png", AssetKind.Texture)]
    [InlineData("Mesh.hlsl", AssetKind.Shader)]
    [InlineData("Mesh.shader.json", AssetKind.Shader)]
    [InlineData("Notes.txt", AssetKind.Other)]
    public void TellsTheKindOfAnAssetFromItsName(string path, AssetKind kind)
    {
        AssetCatalog catalog = _builder.WithFile(path).Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, kind: null, text: "");

        assets.Should().ContainSingle().Which.Kind.Should().Be(kind);
    }

    [Fact]
    public void TellsAMoodByTheSchemaItNames()
    {
        AssetCatalog catalog = _builder
            .WithFile("Moods/Night.json", """
                // A comment first.
                { "$schema": "../.schemas/mood.schema.json", "ambient": 0.1 }
                """)
            .Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("Moods", recursive: false, kind: null, text: "");

        assets.Should().ContainSingle().Which.Kind.Should().Be(AssetKind.Mood);
    }

    [Fact]
    public void ListsAFoldersOwnAssetsSortedByName()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/b.scene.json").WithFile("Scenes/A.scene.json").WithFile("Scenes/Old/C.scene.json").WithFile("Room.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("Scenes", recursive: false, kind: null, text: "");

        assets.Select(static asset => asset.Path).Should().Equal("Scenes/A.scene.json", "Scenes/b.scene.json");
        assets[0].Name.Should().Be("A");
    }

    [Fact]
    public void FiltersByKindAndTextBelowTheFolder()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/Room.scene.json").WithFile("Scenes/Old/Hall.scene.json").WithFile("Hall.glb").Build();

        IReadOnlyList<AssetEntry> assets = catalog.Find("", recursive: true, AssetKind.Scene, text: "hall");

        assets.Select(static asset => asset.Path).Should().Equal("Scenes/Old/Hall.scene.json");
    }

    [Fact]
    public void ListsFoldersAsATreeWithoutHiddenOnes()
    {
        AssetCatalog catalog = _builder.WithFile("Scenes/Old/C.scene.json").WithFile("Moods/Day.json").WithFile(".schemas/scene.schema.json").Build();

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
