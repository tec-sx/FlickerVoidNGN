using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class TimelineViewModelTests
{
    private const double Precision = 1e-6;

    private readonly TimelineViewModelTestsBuilder _builder = new();

    [Fact]
    public void LaysLanesOutWithTheirSubLanesUnderThem()
    {
        TimelineViewModel timeline = _builder.Build();

        IReadOnlyList<TimelineRow> rows = timeline.GetRows();

        rows.Select(row => row.Lane.Name).Should().Equal("Bass", "fills", "Keys");
        rows.Select(row => row.Top).Should().Equal(0, 84, 114);
        rows.Select(row => row.Depth).Should().Equal(0, 1, 0);
        timeline.ContentHeight.Should().Be(154);
    }

    [Fact]
    public void EndsAtTheLatestItem()
    {
        TimelineViewModel timeline = _builder.Build();

        timeline.End.Should().Be(6);
    }

    [Fact]
    public void SelectsOneItemOrAddsAndTakesOut()
    {
        TimelineViewModel timeline = _builder.Build();

        timeline.Select(_builder.Clip);
        timeline.Select(_builder.Key, add: true);
        timeline.Select(_builder.Clip, add: true);

        timeline.SelectedItems.Should().Equal(_builder.Key);
    }

    [Fact]
    public void TellsWhenTheSelectionChanges()
    {
        TimelineViewModel timeline = _builder.Build();
        int changes = 0;
        timeline.SelectionChanged += () => changes++;

        timeline.Select(_builder.Clip);
        timeline.Select(null);

        changes.Should().Be(2);
    }

    [Fact]
    public void DrawsAgainAfterAnInvalidation()
    {
        TimelineViewModel timeline = _builder.Build();
        int revision = timeline.Revision;

        timeline.Invalidate();

        timeline.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void KeepsTheCursorAfterTheStart()
    {
        TimelineViewModel timeline = _builder.Build();

        timeline.MoveCursor(-2);

        timeline.Cursor.Should().Be(0);
    }

    [Fact]
    public void WrapsTheCursorInADay()
    {
        TimelineViewModel timeline = _builder.InHours().Build();

        timeline.MoveCursor(26);

        timeline.Cursor.Should().BeApproximately(2, Precision);
    }

    [Fact]
    public void FitsEverythingWithItsInsetFree()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.FitInset = 10;

        timeline.Fit();

        timeline.Range.ToX(0).Should().BeApproximately(10, Precision);
        timeline.Range.ToX(6).Should().BeApproximately(190, Precision);
    }

    [Fact]
    public void FitsADayWhole()
    {
        TimelineViewModel timeline = _builder.InHours().Build();

        timeline.Fit();

        timeline.Range.MinTime.Should().Be(0);
        timeline.Range.MaxTime.Should().Be(24);
    }

    [Fact]
    public void KeepsItsFitWhenResizedIfAsked()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.FitInset = 10;
        timeline.KeepsFit = true;

        timeline.Range.Resize(400);

        timeline.Range.ToX(6).Should().BeApproximately(390, Precision);
    }
}
