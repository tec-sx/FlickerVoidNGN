namespace FlickerVoid.Editor.Assets.Tests;

public sealed class AssetImportTests : IDisposable
{
    private readonly AssetBrowserViewModelTestsBuilder _builder = new();
    private readonly string _outside = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AssetImportTests()
    {
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        _builder.Dispose();
        Directory.Delete(_outside, recursive: true);
    }

    [Fact]
    public void ImportsFilesFromOutsideIntoTheSelectedFolder()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.WithFile("Paintings/Old.reb").Build();
        browser.SelectedFolder = browser.Folders[0].Children[0];

        browser.Import([wall]);

        _builder.ReadFile("Paintings/Wall.reb").Should().Be("first");
        browser.SelectedAsset!.Path.Should().Be("Paintings/Wall.reb");
    }

    [Fact]
    public void ImportsFilesDroppedOnAFolderIntoThatFolder()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.WithFile("Paintings/Old.reb").Build();

        browser.Import([wall], "Paintings");

        _builder.ReadFile("Paintings/Wall.reb").Should().Be("first");
    }

    [Fact]
    public void ReimportsAFileFromWhereItCameFrom()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.Build();
        browser.Import([wall]);
        WriteOutside("Wall.reb", "repainted");

        browser.CanReimport.Should().BeTrue();
        browser.Reimport();

        _builder.ReadFile("Wall.reb").Should().Be("repainted");
    }

    [Fact]
    public void ImportingTheSameFileAgainReplacesIt()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.Build();
        browser.Import([wall]);
        WriteOutside("Wall.reb", "repainted");

        browser.Import([wall]);

        _builder.ReadFile("Wall.reb").Should().Be("repainted");
        browser.Assets.Select(static asset => asset.Path).Should().Equal("Wall.reb");
    }

    [Fact]
    public void GivesAnotherFileWithATakenNameAFreeName()
    {
        string wall = WriteOutside("Wall.reb", "imported");
        AssetBrowserViewModel browser = _builder.WithFile("Wall.reb", "mine").Build();

        browser.Import([wall]);

        _builder.ReadFile("Wall.reb").Should().Be("mine");
        _builder.ReadFile("Wall 2.reb").Should().Be("imported");
    }

    [Fact]
    public void KeepsWhereAFileCameFromThroughARename()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.Build();
        browser.Import([wall]);
        browser.Rename("Cellar Wall");
        WriteOutside("Wall.reb", "repainted");

        browser.Reimport();

        _builder.ReadFile("Cellar Wall.reb").Should().Be("repainted");
    }

    [Fact]
    public void CannotReimportAFileThatWasNotImported()
    {
        AssetBrowserViewModel browser = _builder.WithFile("Paper.png").Build();

        browser.SelectedAsset = browser.Assets[0];

        browser.CanReimport.Should().BeFalse();
    }

    [Fact]
    public void SaysWhenTheSourceIsGone()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.Build();
        browser.Import([wall]);
        File.Delete(wall);

        browser.Reimport();

        browser.Status.Should().Contain(wall);
        _builder.ReadFile("Wall.reb").Should().Be("first");
    }

    [Fact]
    public void KeepsTheSourcesOutOfSight()
    {
        string wall = WriteOutside("Wall.reb", "first");
        AssetBrowserViewModel browser = _builder.Build();

        browser.Import([wall]);

        browser.Assets.Select(static asset => asset.Path).Should().Equal("Wall.reb");
    }

    private string WriteOutside(string name, string text)
    {
        string fullPath = Path.Combine(_outside, name);
        File.WriteAllText(fullPath, text);
        return fullPath;
    }
}
