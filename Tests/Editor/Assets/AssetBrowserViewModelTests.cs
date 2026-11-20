namespace FlickerVoid.Editor.Assets.Tests;

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
    public void ListsTheSubfoldersBeforeTheFilesOfTheSelectedFolder()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").WithFile("Scenes/Room.fvscene").WithFile("Moods/Night.fvmood").Build();

        browser.Items.Select(static item => item.Path).Should().Equal("Moods", "Scenes", "Room.glb");
        browser.Items[0].Kind.Should().Be(AssetKind.Folder);
    }

    [Fact]
    public void ListsNoFoldersWhileSearching()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.glb").WithFile("Scenes/Room.fvscene").Build();

        browser.SearchText = "room";

        browser.Items.Select(static item => item.Path).Should().Equal("Room.glb", "Scenes/Room.fvscene");
    }

    [Fact]
    public void OpeningAFolderItemShowsItsContents()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Scenes/Room.fvscene").Build();
        AssetEntry? opened = null;
        browser.Opened += asset => opened = asset;

        browser.OpenCommand.Execute(browser.Items[0]);

        opened.Should().BeNull();
        browser.SelectedFolder!.Path.Should().Be("Scenes");
        browser.Items.Select(static item => item.Path).Should().Equal("Scenes/Room.fvscene");
    }

    [Fact]
    public void GoesUpToTheParentFolder()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Scenes/Old/Room.fvscene").Build();
        browser.SelectedFolder = browser.Folders[0].Children[0].Children[0];

        browser.UpCommand.Execute(null);

        browser.SelectedFolder!.Path.Should().Be("Scenes");
    }

    [Fact]
    public void MovesAFileIntoAFolder()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.fvscene").WithFile("Scenes/Hall.fvscene").Build();
        AssetEntry room = browser.Assets[0];

        browser.MoveTo(room, "Scenes");

        browser.Items.Select(static item => item.Path).Should().Equal("Scenes");
        _builder.Exists("Scenes/Room.fvscene").Should().BeTrue();
    }

    [Fact]
    public void MovesAFolderIntoAnotherButNeverIntoItself()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Moods/Night.fvmood").WithFile("Scenes/Hall.fvscene").Build();
        AssetEntry moods = browser.Items[0];

        browser.MoveTo(moods, "Moods");
        browser.Status.Should().NotBeEmpty();
        browser.MoveTo(moods, "Scenes");

        _builder.Exists("Scenes/Moods/Night.fvmood").Should().BeTrue();
        browser.Folders[0].Children.Should().ContainSingle().Which.Children.Should().ContainSingle().Which.Path.Should().Be("Scenes/Moods");
    }

    [Fact]
    public void RenamesAndDeletesASelectedFolderItem()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Old/Room.fvscene").Build();
        browser.SelectedAsset = browser.Items[0];

        browser.RenameCommand.Execute("New");

        browser.Items.Select(static item => item.Path).Should().Equal("New");
        browser.Folders[0].Children.Should().ContainSingle().Which.Path.Should().Be("New");
        browser.SelectedAsset = browser.Items[0];
        browser.DeleteCommand.Execute(null);
        browser.Items.Should().BeEmpty();
        browser.Folders[0].Children.Should().BeEmpty();
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

    [Theory]
    [InlineData("", "Room.glb")]
    [InlineData("Shared", "Shared/Room.glb")]
    public void ShowsTheSourceFoldersOfADevelopmentBuildByTheirContentPaths(string outputFolder, string path)
    {
        AssetBrowserViewModel browser = _builder.WithFile("Room.fvmodel").WithSourceFile(outputFolder, "Room.glb").Build();

        browser.SelectedFilter = browser.Filters.Single(static filter => filter.Kind == AssetKind.Model);

        browser.Assets.Select(static asset => asset.Path).Should().Equal(path);
    }
}
