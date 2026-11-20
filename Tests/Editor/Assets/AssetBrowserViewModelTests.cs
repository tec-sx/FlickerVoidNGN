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
}
