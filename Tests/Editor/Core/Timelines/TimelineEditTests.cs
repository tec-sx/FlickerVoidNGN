using FlickerVoid.Editor.Core.Timelines;

namespace FlickerVoid.Editor.Core.Tests.Timelines;

public sealed class TimelineEditTests
{
    private const double Precision = 1e-6;

    private readonly TimelineEditTestsBuilder _builder = new();

    [Theory]
    [InlineData(200, 40, TimelineHitKind.Item)]
    [InlineData(130, 40, TimelineHitKind.ItemStart)]
    [InlineData(380, 40, TimelineHitKind.ItemEnd)]
    [InlineData(516, 40, TimelineHitKind.Item)]
    [InlineData(900, 40, TimelineHitKind.Lane)]
    [InlineData(360, 99, TimelineHitKind.Item)]
    [InlineData(200, 99, TimelineHitKind.Lane)]
    [InlineData(200, 500, TimelineHitKind.None)]
    public void HitsWhatIsUnderThePointerOnTheLanes(double x, double y, TimelineHitKind kind)
    {
        TimelineViewModel timeline = _builder.Build();

        TimelineHit hit = TimelineEdit.HitLanes(timeline, x, y);

        hit.Kind.Should().Be(kind);
    }

    [Fact]
    public void FindsTheSubLaneAndItsItem()
    {
        TimelineViewModel timeline = _builder.Build();

        TimelineHit hit = TimelineEdit.HitLanes(timeline, 360, 99);

        hit.Row!.Value.Depth.Should().Be(1);
        hit.Item.Should().BeSameAs(_builder.Fill);
    }

    [Fact]
    public void KeepsTheBodyOfANarrowItemGrabbable()
    {
        TimelineViewModel timeline = _builder.Build();
        _builder.Clip.Length = 0.125;

        TimelineHit hit = TimelineEdit.HitLanes(timeline, 132, 40);

        hit.Kind.Should().Be(TimelineHitKind.Item);
    }

    [Theory]
    [InlineData(100, TimelineHitKind.RegionTab, "A")]
    [InlineData(254, TimelineHitKind.RegionEnd, "A")]
    [InlineData(514, TimelineHitKind.RegionStart, "B")]
    [InlineData(600, TimelineHitKind.RegionTab, "B")]
    [InlineData(450, TimelineHitKind.Ruler, null)]
    public void HitsRegionsOnTheRuler(double x, TimelineHitKind kind, string? region)
    {
        TimelineViewModel timeline = _builder.Build();

        TimelineHit hit = TimelineEdit.HitRuler(timeline, x);

        hit.Kind.Should().Be(kind);
        hit.Region?.Name.Should().Be(region);
    }

    [Fact]
    public void MovesAnItemSnappedToBeats()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 200, 40);

        drag.Move(250, snap: true);

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.MoveItem, _builder.Clip, null, 2.75, 4));
        _builder.Clip.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void MovesAnItemFreelyWithoutSnapping()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 200, 40);

        drag.Move(250, snap: false);

        _builder.Editor.Last.Start.Should().BeApproximately(2 + (50.0 / 64), Precision);
    }

    [Fact]
    public void LeavesAnItemWhereItIsForAMoveUnderAPixel()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 200, 40);

        drag.Move(200.5, snap: true);

        _builder.Editor.Changes.Should().BeEmpty();
    }

    [Fact]
    public void NeverMovesAnItemBeforeTheStart()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 200, 40);

        drag.Move(-500, snap: true);

        _builder.Editor.Last.Start.Should().Be(0);
    }

    [Fact]
    public void ShortensAnItemToOneSnapStepAtLeast()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 380, 40);

        drag.Move(100, snap: true);

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.ResizeItem, _builder.Clip, null, 2, 0.25));
    }

    [Fact]
    public void LengthensAnItemPastTheEndShown()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 380, 40);

        drag.Move(2000, snap: true);

        _builder.Editor.Last.Length.Should().BeApproximately(29.25, Precision);
    }

    [Fact]
    public void MovesAnItemsStartKeepingItsEnd()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 130, 40);

        drag.Move(400, snap: true);

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.ResizeItem, _builder.Clip, null, 5.75, 0.25));
    }

    [Fact]
    public void CompletesADragOnceWhenReleased()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = Press(timeline, 200, 40);

        drag.Move(250, snap: true);
        drag.Move(300, snap: true);
        drag.Release();

        _builder.Editor.Completions.Should().Be(1);
    }

    [Fact]
    public void StopsAMovedRegionAtItsNeighbour()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = PressRuler(timeline, 100);

        drag.Move(400, snap: true);

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.MoveRegion, null, _builder.A, 4, 4));
        _builder.A.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void StopsAResizedRegionAtItsNeighbour()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = PressRuler(timeline, 254);

        drag.Move(700, snap: true);

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.ResizeRegion, null, _builder.A, 0, 8));
    }

    [Fact]
    public void LetsRegionsOverlapWhenAllowed()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.RegionsOverlap = true;
        TimelineDrag drag = PressRuler(timeline, 254);

        drag.Move(700, snap: true);

        _builder.Editor.Last.Length.Should().BeApproximately(11, Precision);
    }

    [Fact]
    public void DrawsANewRegionAlongTheRuler()
    {
        TimelineViewModel timeline = _builder.CreatingRegions().Build();
        TimelineDrag drag = PressRuler(timeline, 800);

        drag.Move(960, snap: true);
        TimelineRegion? draft = timeline.Draft;
        drag.Release();

        draft!.Start.Should().BeApproximately(12.5, Precision);
        draft.Length.Should().BeApproximately(2.5, Precision);
        _builder.Editor.Changes.Should().Equal(new TimelineChange(TimelineChangeKind.CreateRegion, null, null, 12.5, 2.5));
        _builder.Editor.Completions.Should().Be(1);
        timeline.Draft.Should().BeNull();
    }

    [Fact]
    public void StopsANewRegionAtItsNeighbour()
    {
        TimelineViewModel timeline = _builder.CreatingRegions().Build();
        TimelineDrag drag = PressRuler(timeline, 780);

        drag.Move(300, snap: true);
        drag.Release();

        _builder.Editor.Last.Should().Be(new TimelineChange(TimelineChangeKind.CreateRegion, null, null, 12, 0.25));
    }

    [Fact]
    public void ScrubsForAShortDragOnTheRuler()
    {
        TimelineViewModel timeline = _builder.CreatingRegions().Build();
        TimelineDrag drag = PressRuler(timeline, 450);

        drag.Move(452, snap: true);
        drag.Release();

        timeline.Cursor.Should().BeApproximately(7, Precision);
        _builder.Editor.Changes.Should().BeEmpty();
        _builder.Editor.Completions.Should().Be(0);
    }

    [Fact]
    public void ScrubsAlongTheRulerWhenRegionsAreNotDrawn()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineDrag drag = PressRuler(timeline, 450);

        drag.Move(610, snap: true);

        timeline.Cursor.Should().BeApproximately(9.5, Precision);
        timeline.Draft.Should().BeNull();
    }

    [Fact]
    public void ClearsTheSelectionAndScrubsOnAnEmptyLane()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.Select(_builder.Clip);

        Press(timeline, 900, 40);

        _builder.Clip.IsSelected.Should().BeFalse();
        timeline.Cursor.Should().BeApproximately(14, Precision);
    }

    [Fact]
    public void WrapsTheCursorAroundTheDay()
    {
        TimelineViewModel timeline = _builder.InHours().Build();
        timeline.Range.Show(0, 32);
        TimelineDrag drag = PressRuler(timeline, 200);

        drag.Move(800, snap: true);

        timeline.Cursor.Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void AddsToTheSelectionWithControl()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.Select(_builder.Clip);
        TimelineHit hit = TimelineEdit.HitLanes(timeline, 512, 40);

        TimelineEdit.Press(timeline, hit, 512, snap: true, add: true);

        timeline.SelectedItems.Should().BeEquivalentTo([_builder.Clip, _builder.Key]);
    }

    [Fact]
    public void NudgesTheSelectedItemsBySnapSteps()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.Select(_builder.Clip);

        TimelineEdit.Nudge(timeline, -1);

        _builder.Editor.Changes.Should().Equal(new TimelineChange(TimelineChangeKind.MoveItem, _builder.Clip, null, 1.75, 4));
        _builder.Editor.Completions.Should().Be(1);
    }

    [Fact]
    public void DeletesTheSelectedItemsAndRegion()
    {
        TimelineViewModel timeline = _builder.Build();
        timeline.Select(_builder.Clip);
        timeline.Select(_builder.Key, add: true);

        TimelineEdit.DeleteSelected(timeline);

        _builder.Editor.Changes.Select(change => change.Kind).Should().Equal(TimelineChangeKind.DeleteItem, TimelineChangeKind.DeleteItem);
        _builder.Editor.Completions.Should().Be(1);
    }

    [Fact]
    public void LeavesAnItemThatCannotMove()
    {
        TimelineViewModel timeline = _builder.Build();
        TimelineItem fixedItem = new() { Start = 14, Length = 1, CanMove = false };
        timeline.Lanes[0].Items.Add(fixedItem);
        TimelineDrag drag = Press(timeline, 920, 40);

        drag.Move(1000, snap: true);

        _builder.Editor.Changes.Should().BeEmpty();
        fixedItem.IsSelected.Should().BeTrue();
    }

    private static TimelineDrag Press(TimelineViewModel timeline, double x, double y)
    {
        TimelineHit hit = TimelineEdit.HitLanes(timeline, x, y);
        return TimelineEdit.Press(timeline, hit, x, snap: true, add: false);
    }

    private static TimelineDrag PressRuler(TimelineViewModel timeline, double x)
    {
        TimelineHit hit = TimelineEdit.HitRuler(timeline, x);
        return TimelineEdit.Press(timeline, hit, x, snap: true, add: false);
    }
}
