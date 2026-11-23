using System.Numerics;

using FlickerVoid.Engine.Cameras.Cutaways;
using FlickerVoid.Engine.Rendering.Extraction;

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
    public void ClosesTheBuildingOnLeaving()
    {
        _builder.WalkTo(Vector3.Zero);

        _builder.WalkTo(new Vector3(20, 0, 0));

        _builder.World.Has<Cutaway>(_builder["Ground"]).Should().BeFalse();
        ShadowOnly("Ceiling").Should().BeFalse();
        ShadowOnly("Roof").Should().BeFalse();
    }

    private bool ShadowOnly(string name) => _builder.World.Has<ShadowOnly>(_builder[name]);
}
