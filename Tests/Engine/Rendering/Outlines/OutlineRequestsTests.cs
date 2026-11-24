using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Rendering.Outlines;

namespace FlickerVoid.Engine.Rendering.Tests.Outlines;

public sealed class OutlineRequestsTests : IDisposable
{
    private static readonly Tag Highlight = new("Outline.Highlight");
    private static readonly Tag Scan = new("Outline.Scan");
    private static readonly Vector3 Yellow = new(1, 1, 0);
    private static readonly Vector3 Cyan = new(0, 1, 1);
    private static readonly Vector3 Red = new(1, 0, 0);

    private readonly OutlineRequestsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheHighestPriorityRequestOutlinesTheEntityAndWithdrawingItShowsTheNext()
    {
        Entity door = _builder.Spawn();

        OutlineRequests.Request(_builder.World, door, Highlight, Yellow);
        OutlineRequests.Request(_builder.World, door, Scan, Cyan, priority: 1);
        Vector3? both = _builder.ColorOf(door);
        OutlineRequests.Withdraw(_builder.World, door, Scan);

        both.Should().Be(Cyan);
        _builder.ColorOf(door).Should().Be(Yellow);
        OutlineRequests.IsRequested(_builder.World, door, Highlight).Should().BeTrue();
        OutlineRequests.IsRequested(_builder.World, door, Scan).Should().BeFalse();
    }

    [Fact]
    public void WithdrawingEveryRequestGivesBackTheOutlineTheEntityHadOrNone()
    {
        Entity marked = _builder.Spawn(Red);
        Entity plain = _builder.Spawn();

        OutlineRequests.Request(_builder.World, marked, Highlight, Yellow);
        OutlineRequests.Request(_builder.World, plain, Highlight, Yellow);
        Vector3? requested = _builder.ColorOf(marked);
        OutlineRequests.Withdraw(_builder.World, marked, Highlight);
        OutlineRequests.Withdraw(_builder.World, plain, Highlight);

        requested.Should().Be(Yellow);
        _builder.ColorOf(marked).Should().Be(Red, "the outline it had in the scene is kept under the requests");
        _builder.ColorOf(plain).Should().BeNull();
    }

    [Fact]
    public void ARequestAgainFromTheSameSourceReplacesItAndTheLatestOfEqualPrioritiesShows()
    {
        Entity door = _builder.Spawn();

        OutlineRequests.Request(_builder.World, door, Highlight, Yellow);
        OutlineRequests.Request(_builder.World, door, Scan, Cyan);
        Vector3? latest = _builder.ColorOf(door);
        OutlineRequests.Request(_builder.World, door, Scan, Red);
        Vector3? replaced = _builder.ColorOf(door);
        OutlineRequests.Withdraw(_builder.World, door, Scan);

        latest.Should().Be(Cyan);
        replaced.Should().Be(Red);
        _builder.ColorOf(door).Should().Be(Yellow);
    }
}
