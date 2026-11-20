using FlickerVoid.Engine.Editor.Assets;

namespace FlickerVoid.Engine.Editor.Tests.Assets;

public sealed class AssetBrowserViewModelTests : IDisposable
{
    private readonly AssetBrowserViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsTheContentFolderAndItsOwnFilesAtFirst()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").WithFile("Scenes/Room.fvscene").Build();

        browser.Folders.Should().ContainSingle().Which.Children.Should().ContainSingle().Which.Name.Should().Be("Scenes");
        browser.Assets.Select(static asset => asset.Path).Should().Equal("Room.glb");
    }

    [Fact]
    public void ShowsTheFilesOfTheSelectedFolder()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").WithFile("Scenes/Room.fvscene").Build();

        browser.SelectedFolder = browser.Folders[0].Children[0];

        browser.Assets.Select(static asset => asset.Path).Should().Equal("Scenes/Room.fvscene");
    }

    [Fact]
    public void SearchesAndFiltersEverythingBelowTheSelectedFolder()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").WithFile("Scenes/Room.fvscene").WithFile("Scenes/Hall.fvscene").Build();

        browser.SelectedFilter = browser.Filters.Single(static filter => filter.Kind == AssetKind.Scene);
        browser.Assets.Should().HaveCount(2);
        browser.SearchText = "room";

        browser.Assets.Select(static asset => asset.Path).Should().Equal("Scenes/Room.fvscene");
    }

    [Fact]
    public void OpeningAnAssetRaisesOpened()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").Build();
        AssetEntry? opened = null;
        browser.Opened += asset => opened = asset;

        browser.OpenCommand.Execute(browser.Assets[0]);

        opened!.Path.Should().Be("Room.glb");
    }

    [Fact]
    public void RefreshFindsNewFiles()
    {
        AssetBrowserViewModel browser = _builder.Build();
        _builder.WithFile("Room.glb");

        browser.RefreshCommand.Execute(null);

        browser.Assets.Should().ContainSingle();
    }

    [Fact]
    public void CreatesAnEmptySceneInTheSelectedFolderAndSelectsIt()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Scenes/Room.fvscene").Build();
        browser.SelectedFolder = browser.Folders[0].Children[0];

        browser.CreateSceneCommand.Execute(null);

        browser.SelectedAsset!.Path.Should().Be("Scenes/New Scene.fvscene");
        browser.Assets.Should().Contain(browser.SelectedAsset);
        _builder.ReadFile("Scenes/New Scene.fvscene").Should().Contain("\"$schema\": \"../.schemas/scene.schema.json\"").And.Contain("\"entities\": []");
    }

    [Fact]
    public void NumbersANewFileWhoseNameIsTaken()
    {
        AssetBrowserViewModel browser = _builder.Build();

        browser.CreateMaterialCommand.Execute(null);
        browser.CreateMaterialCommand.Execute(null);

        browser.Assets.Select(static asset => asset.Path).Should().Equal("New Material 2.fvmat", "New Material.fvmat");
        _builder.ReadFile("New Material.fvmat").Should().Contain("\"parameters\": {}");
    }

    [Fact]
    public void CreatesMoodsAndFolders()
    {
        AssetBrowserViewModel browser = _builder.Build();

        browser.CreateMoodCommand.Execute(null);
        browser.Assets.Select(static asset => asset.Path).Should().Equal("New Mood.fvmood");

        browser.CreateFolderCommand.Execute(null);

        browser.SelectedFolder!.Path.Should().Be("New Folder");
        browser.Folders[0].Children.Should().ContainSingle();
    }

    [Fact]
    public void RenamesTheSelectedAssetKeepingItsExtension()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.fvscene").Build();
        browser.SelectedAsset = browser.Assets[0];

        browser.RenameCommand.Execute("Hall");

        browser.Assets.Select(static asset => asset.Path).Should().Equal("Hall.fvscene");
        browser.SelectedAsset!.Path.Should().Be("Hall.fvscene");
    }

    [Fact]
    public void RenamesTheSelectedFolderWhenNoAssetIsSelected()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Old/Room.fvscene").Build();
        browser.SelectedFolder = browser.Folders[0].Children[0];

        browser.RenameCommand.Execute("New");

        browser.SelectedFolder!.Path.Should().Be("New");
        browser.Assets.Select(static asset => asset.Path).Should().Equal("New/Room.fvscene");
    }

    [Fact]
    public void RefusesANameThatIsTakenOrInvalid()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.fvscene").WithFile("Hall.fvscene").Build();
        browser.SelectedAsset = browser.Assets[1];

        browser.RenameCommand.Execute("Hall");
        browser.RenameCommand.Execute("a/b");

        browser.Assets.Select(static asset => asset.Path).Should().Equal("Hall.fvscene", "Room.fvscene");
        browser.Status.Should().NotBeEmpty();
    }

    [Fact]
    public void DeletesTheSelectedAsset()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.fvscene").Build();
        browser.SelectedAsset = browser.Assets[0];

        browser.DeleteCommand.Execute(null);

        browser.Assets.Should().BeEmpty();
        _builder.Exists("Room.fvscene").Should().BeFalse();
    }
}
