using System.Numerics;

using FlickerVoid.Editor.Core.Buildings;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

public sealed class PlanTagLayoutTests
{
    private static readonly Vector2 Size = new(100, 20);

    [Fact]
    public void TagsThatStandClearStayWhereTheyPoint()
    {
        PlanTag[] tags = [new(new Vector2(100, 100), Size, PlanLabelKind.Dimension), new(new Vector2(260, 100), Size, PlanLabelKind.Dimension)];

        Vector2[] placed = PlanTagLayout.Spread(tags);

        placed.Should().Equal(new Vector2(100, 100), new Vector2(260, 100));
    }

    [Fact]
    public void ATagOverAnotherStepsBelowIt()
    {
        PlanTag[] tags = [new(new Vector2(100, 100), Size, PlanLabelKind.Dimension), new(new Vector2(150, 108), Size, PlanLabelKind.Dimension)];

        Vector2[] placed = PlanTagLayout.Spread(tags);

        placed[0].Should().Be(new Vector2(100, 100));
        placed[1].X.Should().Be(150);
        placed[1].Y.Should().Be(100 + Size.Y + PlanTagLayout.Gap, "it stands under the first with a gap between them");
    }

    [Fact]
    public void TheDragsTagKeepsItsPlaceAndTheOthersMakeWay()
    {
        PlanTag[] tags = [new(new Vector2(100, 100), Size, PlanLabelKind.Dimension), new(new Vector2(110, 104), Size, PlanLabelKind.Drag)];

        Vector2[] placed = PlanTagLayout.Spread(tags);

        placed[1].Should().Be(new Vector2(110, 104), "the tag of what is in hand is the one being read");
        placed[0].Y.Should().Be(104 + Size.Y + PlanTagLayout.Gap);
    }

    [Fact]
    public void ARoomsNameStaysInItsRoomAndATagOverItStepsBelow()
    {
        Vector2 room = PlanTagLayout.MeasureRoom("Hall", "34.3 m²");
        PlanTag[] tags = [new(new Vector2(100, 96), Size, PlanLabelKind.Dimension), new(new Vector2(100, 100), room, PlanLabelKind.Room)];

        Vector2[] placed = PlanTagLayout.Spread(tags);

        placed[1].Should().Be(new Vector2(100, 100));
        placed[0].Y.Should().Be(100 + ((Size.Y + room.Y) / 2) + PlanTagLayout.Gap);
    }

    [Fact]
    public void ATagSteppingDownClearsEveryTagUnderItToo()
    {
        PlanTag[] tags =
        [
            new(new Vector2(100, 100), Size, PlanLabelKind.Dimension),
            new(new Vector2(100, 124), Size, PlanLabelKind.Dimension),
            new(new Vector2(100, 102), Size, PlanLabelKind.Dimension),
        ];

        Vector2[] placed = PlanTagLayout.Spread(tags);

        placed[2].Y.Should().Be(124 + Size.Y + PlanTagLayout.Gap, "below the first it would stand on the second");
    }

    [Fact]
    public void ATagsSizeIsGuessedFromWhatItSays()
    {
        Vector2 small = PlanTagLayout.Measure("Lift", string.Empty);
        Vector2 wide = PlanTagLayout.Measure("3.50 m along", "drag or click another wall");

        wide.X.Should().BeGreaterThan(small.X * 3);
        wide.Y.Should().Be(small.Y);
    }
}
