using FlickerVoid.Editor.Core.Tags;

namespace FlickerVoid.Editor.Core.Tests.Tags;

public sealed class TagPickerViewModelTests
{
    private readonly TagCatalog _catalog = new();
    private List<string> _picked = ["Music.Drums"];

    public TagPickerViewModelTests()
    {
        _catalog.Add("Music.Drums.Inn");
        _catalog.Add("Music.Drums.Cellar");
        _catalog.Add("Music.Hats");
        _catalog.Add("Ambience.Rain");
        _catalog.SetUses(new Dictionary<string, int> { ["Music.Drums"] = 2 });
    }

    private TagPickerViewModel Build(bool single = false)
    {
        return new TagPickerViewModel(_catalog, () => _picked, picked => _picked = [.. picked], single);
    }

    private static TagPickerRowViewModel Row(TagPickerViewModel picker, string path) => picker.Rows.First(row => row.Path == path);

    [Fact]
    public void OpensWithThePickedTagsShownAndOtherBranchesFolded()
    {
        TagPickerViewModel picker = Build();

        picker.Rows.Select(row => row.Path).Should().Equal("Ambience", "Music", "Music.Drums", "Music.Drums.Cellar", "Music.Drums.Inn", "Music.Hats");
        Row(picker, "Ambience").IsFolded.Should().BeTrue();
        Row(picker, "Ambience").Note.Should().Be("1", "a folded parent says how many tags it holds");
        picker.PickedText.Should().Be("1 picked");
    }

    [Fact]
    public void MarksPickedTagsAndTheirParents()
    {
        TagPickerViewModel picker = Build();

        Row(picker, "Music.Drums").IsOn.Should().BeTrue();
        Row(picker, "Music").IsPart.Should().BeTrue("something below it is picked");
        Row(picker, "Music.Hats").IsOn.Should().BeFalse();
        Row(picker, "Music.Drums").Note.Should().Be("used 2 times");
        Row(picker, "Music.Drums").Indent.Should().Be(8 + 15);
    }

    [Fact]
    public void PicksAndUnpicksATagWithAClick()
    {
        TagPickerViewModel picker = Build();

        picker.ToggleCommand.Execute(Row(picker, "Music.Hats"));
        _picked.Should().Equal("Music.Drums", "Music.Hats");
        Row(picker, "Music.Hats").IsOn.Should().BeTrue();

        picker.ToggleCommand.Execute(Row(picker, "Music.Drums"));
        _picked.Should().Equal("Music.Hats");
        picker.PickedText.Should().Be("1 picked");
    }

    [Fact]
    public void PicksOnlyOneTagForAFieldThatHoldsOne()
    {
        TagPickerViewModel picker = Build(single: true);

        picker.ToggleCommand.Execute(Row(picker, "Music.Hats"));

        _picked.Should().Equal("Music.Hats");
    }

    [Fact]
    public void FoldsAndOpensABranch()
    {
        TagPickerViewModel picker = Build();

        picker.FoldCommand.Execute(Row(picker, "Music"));
        picker.Rows.Select(row => row.Path).Should().Equal("Ambience", "Music");

        picker.FoldCommand.Execute(Row(picker, "Ambience"));
        picker.Rows.Select(row => row.Path).Should().Equal("Ambience", "Ambience.Rain", "Music");
    }

    [Fact]
    public void SearchesTheWholeTreeWhateverIsFolded()
    {
        TagPickerViewModel picker = Build();

        picker.Search = "rain";

        picker.Rows.Select(row => row.Path).Should().Equal("Ambience", "Ambience.Rain");
    }

    [Fact]
    public void AddsANewTagPicksItAndShowsIt()
    {
        TagPickerViewModel picker = Build();
        picker.NewTag = "Ambience.Wind";

        picker.AddTagCommand.Execute(null);

        _catalog.Contains("Ambience.Wind").Should().BeTrue();
        _picked.Should().Equal("Music.Drums", "Ambience.Wind");
        Row(picker, "Ambience.Wind").IsOn.Should().BeTrue();
        picker.NewTag.Should().BeEmpty();
    }

    [Fact]
    public void StartsANewTagUnderARow()
    {
        TagPickerViewModel picker = Build();

        picker.AddBelowCommand.Execute(Row(picker, "Music.Hats"));

        picker.NewTag.Should().Be("Music.Hats.");
    }

    [Fact]
    public void RenamesATagInPlaceAndKeepsItPicked()
    {
        TagPickerViewModel picker = Build();
        TagPickerRowViewModel drums = Row(picker, "Music.Drums");

        picker.StartRenameCommand.Execute(drums);
        drums.IsRenaming.Should().BeTrue();
        drums.RenameText.Should().Be("Music.Drums");
        drums.RenameText = "Music.Beats";
        picker.CommitRenameCommand.Execute(drums);

        _catalog.Contains("Music.Beats.Inn").Should().BeTrue();
        _picked.Should().Equal("Music.Beats");
        Row(picker, "Music.Beats").IsOn.Should().BeTrue();
    }

    [Fact]
    public void AsksBeforeRemovingATagInUse()
    {
        TagPickerViewModel picker = Build();

        picker.RemoveCommand.Execute(Row(picker, "Music.Drums"));

        _catalog.Contains("Music.Drums").Should().BeTrue("a tag in use is removed only once confirmed");
        picker.Asking.Should().Be("Music.Drums is used 2 times. Remove it and the tags below it from every file?");

        picker.ConfirmRemoveCommand.Execute(null);

        _catalog.Contains("Music.Drums").Should().BeFalse();
        _picked.Should().BeEmpty();
        picker.Asking.Should().BeNull();
    }

    [Fact]
    public void RemovesAnUnusedTagAtOnce()
    {
        TagPickerViewModel picker = Build();

        picker.RemoveCommand.Execute(Row(picker, "Music.Hats"));

        _catalog.Contains("Music.Hats").Should().BeFalse();
        picker.Asking.Should().BeNull();
    }
}
