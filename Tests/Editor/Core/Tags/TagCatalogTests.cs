using FlickerVoid.Editor.Core.Tags;

namespace FlickerVoid.Editor.Core.Tests.Tags;

public sealed class TagCatalogTests
{
    private readonly TagCatalog _catalog = new();
    private int _changes;

    public TagCatalogTests()
    {
        _catalog.Changed += () => _changes++;
    }

    [Fact]
    public void AddsATagWithItsParents()
    {
        _catalog.Add("Music.Drums.Inn");

        _catalog.Names.Should().Equal("Music", "Music.Drums", "Music.Drums.Inn");
        _changes.Should().Be(1);
    }

    [Fact]
    public void RefusesAnEmptyOrBadName()
    {
        _catalog.Add("").Should().BeFalse();
        _catalog.Add("Music..Drums").Should().BeFalse();
        _catalog.Add(" Music.Drums ").Should().BeTrue();

        _catalog.Names.Should().Equal("Music", "Music.Drums");
    }

    [Fact]
    public void RemovesATagWithEverythingBelowIt()
    {
        _catalog.Add("Music.Drums.Inn");
        _catalog.Add("Music.Bass");

        _catalog.Remove("Music.Drums");

        _catalog.Names.Should().Equal("Music", "Music.Bass");
    }

    [Fact]
    public void RenamesATagAndItsChildrenAndSaysWhatChanged()
    {
        _catalog.Add("Music.Drums.Inn");

        IReadOnlyList<(string Old, string New)> renamed = _catalog.Rename("Music.Drums", "Music.Beats");

        _catalog.Names.Should().Equal("Music", "Music.Beats", "Music.Beats.Inn");
        renamed.Should().Equal(("Music.Drums", "Music.Beats"), ("Music.Drums.Inn", "Music.Beats.Inn"));
    }

    [Fact]
    public void RefusesARenameOntoATagThatExists()
    {
        _catalog.Add("Music.Drums");
        _catalog.Add("Music.Bass");

        _catalog.Rename("Music.Drums", "Music.Bass").Should().BeEmpty();

        _catalog.Names.Should().Equal("Music", "Music.Bass", "Music.Drums");
    }

    [Fact]
    public void ListsTheTreeInOrderWithDepthsAndFoldsWhatIsCollapsed()
    {
        _catalog.Add("Music.Drums.Inn");
        _catalog.Add("Music.Bass");
        _catalog.Add("Quest.Cellar");

        _catalog.GetRows("", []).Select(row => (row.Path, row.Depth)).Should().Equal(("Music", 0), ("Music.Bass", 1), ("Music.Drums", 1), ("Music.Drums.Inn", 2), ("Quest", 0), ("Quest.Cellar", 1));
        _catalog.GetRows("", ["Music.Drums", "Quest"]).Select(row => row.Path).Should().Equal("Music", "Music.Bass", "Music.Drums", "Quest");
        _catalog.GetRows("inn", ["Music.Drums"]).Select(row => row.Path).Should().Equal("Music", "Music.Drums", "Music.Drums.Inn");
    }

    [Fact]
    public void KeepsATagsChildrenRightUnderItWhateverComesAfterItsName()
    {
        _catalog.Add("Music.Drums");
        _catalog.Add("Music-Old");
        _catalog.Add("Music.Bass");

        _catalog.Names.Should().Equal("Music", "Music.Bass", "Music.Drums", "Music-Old");
    }

    [Fact]
    public void CountsUsesOfATagAndBelowItAndAddsTagsInUse()
    {
        _catalog.SetUses(new Dictionary<string, int> { ["Music.Drums"] = 2, ["Music.Drums.Inn"] = 1 });

        _catalog.Names.Should().Equal("Music", "Music.Drums", "Music.Drums.Inn");
        _catalog.UsesOf("Music.Drums").Should().Be(2);
        _catalog.UsesBelow("Music").Should().Be(3);
        _catalog.CountBelow("Music").Should().Be(2);
    }

    [Fact]
    public void RenamesThroughTheProjectsFilesAndMovesTheUses()
    {
        ITagFiles files = Substitute.For<ITagFiles>();
        files.Rename("Music.Drums", "Music.Beats", out Arg.Any<string?>()).Returns(true);
        _catalog.Files = files;
        _catalog.SetUses(new Dictionary<string, int> { ["Music.Drums"] = 2 });

        _catalog.Rename("Music.Drums", "Music.Beats", out string? why).Should().BeTrue(why);

        _catalog.UsesOf("Music.Beats").Should().Be(2);
        _catalog.Names.Should().Equal("Music", "Music.Beats");
    }

    [Fact]
    public void KeepsATagTheFilesCouldNotRename()
    {
        ITagFiles files = Substitute.For<ITagFiles>();
        files.Rename(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<string?>()).Returns(call =>
        {
            call[2] = "Save your work first.";
            return false;
        });
        _catalog.Files = files;
        _catalog.SetUses(new Dictionary<string, int> { ["Music.Drums"] = 2 });

        _catalog.Rename("Music.Drums", "Music.Beats", out string? why).Should().BeFalse();

        why.Should().Be("Save your work first.");
        _catalog.Names.Should().Equal("Music", "Music.Drums");
    }

    [Fact]
    public void RemovesATagNoFileUsesWithoutAskingTheFiles()
    {
        ITagFiles files = Substitute.For<ITagFiles>();
        _catalog.Files = files;
        _catalog.Add("Music.Drums");

        _catalog.Remove("Music.Drums", out _).Should().BeTrue();

        files.DidNotReceiveWithAnyArgs().Remove(default!, out _);
        _catalog.Names.Should().Equal("Music");
    }
}
