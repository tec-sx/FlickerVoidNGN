using FlickerVoid.Editor.Core.Lists;

namespace FlickerVoid.Editor.Core.Tests.Lists;

public sealed class ListSelectionTests
{
    private readonly ListSelectionTestsBuilder _builder = new();

    [Fact]
    public void AClickSelectsOneRow()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("b");

        selection.Click("d");

        selection.Selected.Should().Equal("d");
    }

    [Fact]
    public void AControlClickAddsOrTakesOutARow()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("b");

        selection.Click("d", control: true);
        selection.Click("e", control: true);
        selection.Click("b", control: true);

        selection.Selected.Should().Equal("d", "e");
    }

    [Fact]
    public void AShiftClickSelectsFromTheAnchor()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("b");

        selection.Click("e", shift: true);
        selection.Click("c", shift: true);

        selection.Selected.Should().Equal("b", "c");
    }

    [Fact]
    public void AControlShiftClickAddsARange()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("a");
        selection.Click("d", control: true);

        selection.Click("f", control: true, shift: true);

        selection.Selected.Should().Equal("a", "d", "e", "f");
    }

    [Fact]
    public void AShiftClickWithNoAnchorStartsAtTheTop()
    {
        ListSelection<string> selection = _builder.Build();

        selection.Click("c", shift: true);

        selection.Selected.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void SelectsEveryShownRow()
    {
        ListSelection<string> selection = _builder.Build();

        selection.SelectAll();

        selection.Selected.Should().Equal(ListSelectionTestsBuilder.Rows);
    }

    [Fact]
    public void ArrowsMoveTheSelectionAndShiftArrowsGrowIt()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("c");

        selection.MoveFocus(1);
        selection.MoveFocus(1, shift: true);
        selection.MoveFocus(1, shift: true);

        selection.Selected.Should().Equal("d", "e", "f");
    }

    [Fact]
    public void ArrowsStopAtTheEnds()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("a");

        selection.MoveFocus(-1);

        selection.Selected.Should().Equal("a");
    }

    [Fact]
    public void ARightClickOutsideTheSelectionSelectsThatRow()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("a");
        selection.Click("b", control: true);

        selection.RightClick("e");

        selection.Selected.Should().Equal("e");
    }

    [Fact]
    public void ARightClickInsideTheSelectionKeepsIt()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Click("a");
        selection.Click("b", control: true);

        selection.RightClick("b");

        selection.Selected.Should().Equal("a", "b");
    }

    [Fact]
    public void AFilterDropsTheRowsItHides()
    {
        ListSelection<string> selection = _builder.Build();
        selection.SelectAll();

        selection.Show(["b", "d"]);

        selection.Selected.Should().Equal("b", "d");
    }

    [Fact]
    public void AShiftClickSpansOnlyTheShownRows()
    {
        ListSelection<string> selection = _builder.Build();
        selection.Show(["a", "c", "e"]);
        selection.Click("a");

        selection.Click("e", shift: true);

        selection.Selected.Should().Equal("a", "c", "e");
    }

    [Fact]
    public void TellsWhenTheSelectionChanges()
    {
        ListSelection<string> selection = _builder.Build();
        int changes = 0;
        selection.Changed += () => changes++;

        selection.Click("a");
        selection.Click("a");

        changes.Should().Be(1);
    }

    [Theory]
    [InlineData(new[] { "a", "b" }, SelectionMark.All)]
    [InlineData(new[] { "a", "c" }, SelectionMark.Some)]
    [InlineData(new[] { "c", "d" }, SelectionMark.None)]
    [InlineData(new string[0], SelectionMark.None)]
    public void MarksAMenuItemByHowManyOfTheSelectionHaveIt(string[] selected, SelectionMark mark)
    {
        HashSet<string> grouped = ["a", "b"];

        SelectionMark result = SelectionMarks.Of(selected, grouped.Contains);

        result.Should().Be(mark);
    }
}
