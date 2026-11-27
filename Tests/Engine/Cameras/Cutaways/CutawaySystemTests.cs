using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Cutaways;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Cameras.Tests.Cutaways;

public sealed class CutawaySystemTests : IDisposable
{
    private readonly CutawaySystemTestsBuilder _builder = new();
    private readonly CutawaySystem _cutaways;

    public CutawaySystemTests()
    {
        _builder.WithHouse();
        _cutaways = _builder.Build();
    }

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void CutsNothingOutside()
    {
        _builder.WalkTo(new Vector3(20, 0, 0));

        _cutaways.Cutaway.Should().BeNull();
        ShadowOnly("Roof").Should().BeFalse();
    }

    [Fact]
    public void OpensTheGroundFloorAndHidesEverythingAboveIt()
    {
        _builder.WalkTo(Vector3.Zero);

        _cutaways.Cutaway.Should().Be(_builder["Ground"]);
        _builder.World.Has<Cutaway>(_builder["Ground"]).Should().BeTrue();
        ShadowOnly("Ceiling").Should().BeTrue();
        ShadowOnly("Upper Wall").Should().BeTrue();
        ShadowOnly("Roof").Should().BeTrue();
        ShadowOnly("Ground Wall").Should().BeFalse("the walls of the storey stay, cut only where they stand in front of it");
        ShadowOnly("Shed Roof").Should().BeFalse("other buildings stay whole");
    }

    [Fact]
    public void OpensTheUpperFloorStandingOnTheCeilingBelow()
    {
        _builder.WalkTo(new Vector3(0, 3.2f, 0));

        _cutaways.Cutaway.Should().Be(_builder["Upper"]);
        _builder.World.Has<Cutaway>(_builder["Ground"]).Should().BeFalse();
        ShadowOnly("Ceiling").Should().BeFalse();
        ShadowOnly("Upper Wall").Should().BeFalse();
        ShadowOnly("Roof").Should().BeTrue();
    }

    [Fact]
    public void HidesWhatIsNestedByEntityAboveAStoreyNestedByEntityAsACreationsPartsAre()
    {
        World world = _builder.World;
        Vector3 place = new(40, 0, 0);
        Entity building = world.Create(new Transform { Position = place });
        Entity storey = world.Create(new Transform { Position = place }, new IndoorVolume { Size = new Vector3(4, 3, 4) }, new NestedUnder(building));
        Entity roof = world.Create(new Transform { Position = place + new Vector3(0, 3, 0) }, new NestedUnder(building));

        _builder.WalkTo(place);

        _cutaways.Cutaway.Should().Be(storey);
        world.Has<ShadowOnly>(roof).Should().BeTrue();
        world.Has<ShadowOnly>(storey).Should().BeFalse();
    }

    [Fact]
    public void FollowsTheLiveShotsTargetWhateverOtherTargetsThereAre()
    {
        _builder.WalkTo(Vector3.Zero);

        _builder.ChangeFoxArchetype();

        _cutaways.Cutaway.Should().Be(_builder["Ground"], "the door the cameras may also look at stands outside");
    }

    [Fact]
    public void ClosesTheBuildingOnLeaving()
    {
        _builder.WalkTo(Vector3.Zero);

        _builder.WalkTo(new Vector3(20, 0, 0));

        _builder.World.Has<Cutaway>(_builder["Ground"]).Should().BeFalse();
        ShadowOnly("Ceiling").Should().BeFalse();
        ShadowOnly("Roof").Should().BeFalse();
    }

    [Fact]
    public void TellsWhatTheCameraCantSee()
    {
        _builder.WalkTo(Vector3.Zero);
        Vector3 down = Vector3.Normalize(new Vector3(0, -1, -1));

        bool roof = _cutaways.IsUnseen(_builder["Roof"], new Vector3(0, 6.2f, 0), down);
        bool wallInFront = _cutaways.IsUnseen(_builder["Shed Roof"], new Vector3(0, 1, 4), down);
        bool behind = _cutaways.IsUnseen(_builder["Shed Roof"], new Vector3(0, 1, -4), down);
        bool inside = _cutaways.IsUnseen(_builder["Ground Wall"], new Vector3(0, 1, 2), down);

        roof.Should().BeTrue("it only casts shadows");
        wallInFront.Should().BeTrue("it stands in front of the room");
        behind.Should().BeFalse("the room is not behind it");
        inside.Should().BeFalse();
    }

    private bool ShadowOnly(string name) => _builder.World.Has<ShadowOnly>(_builder[name]);
}
