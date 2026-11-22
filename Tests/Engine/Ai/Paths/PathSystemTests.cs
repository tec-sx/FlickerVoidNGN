using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Ai.Tests.Paths;

public sealed class PathSystemTests : IDisposable
{
    private static readonly Vector3 West = new(-3, 0, 0);
    private static readonly Vector3 East = new(3, 0, 0);

    private readonly PathSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void GoesStraightWhenNothingIsInTheWay()
    {
        PathSystem paths = _builder.WithFloor().Build();
        List<Vector3> corners = [];

        bool found = paths.TryFindPath(West, East, corners);

        found.Should().BeTrue();
        corners.Should().ContainSingle();
        corners[0].X.Should().BeApproximately(3, 0.1f);
        corners[0].Z.Should().BeApproximately(0, 0.1f);
    }

    [Fact]
    public void GoesAroundAWallKeepingItsRadiusAway()
    {
        PathSystem paths = _builder.WithFloor().WithWall().Build();
        List<Vector3> corners = [];

        bool found = paths.TryFindPath(West, East, corners);

        found.Should().BeTrue();
        corners.Count.Should().BeGreaterThan(1);
        corners.Should().Contain(corner => MathF.Abs(corner.Z) >= 3, "the way round passes the wall's end");
        corners[^1].X.Should().BeApproximately(3, 0.1f);
        corners[^1].Z.Should().BeApproximately(0, 0.1f);
    }

    [Fact]
    public void EndsAsNearAsItCanGetToAPointItCannotReach()
    {
        PathSystem paths = _builder.WithFloor().WithWall().Build();
        List<Vector3> corners = [];
        Vector3 insideWall = new(0, 0, 0);

        bool found = paths.TryFindPath(West, insideWall, corners);

        found.Should().BeTrue();
        corners[^1].X.Should().BeLessThan(-0.2f, "the wall and the agent's radius keep it away");
    }

    [Fact]
    public void FindsNoWayFromOrToWhereThereIsNoGround()
    {
        PathSystem paths = _builder.WithFloor().Build();
        List<Vector3> corners = [];
        Vector3 farAway = new(40, 0, 0);

        bool found = paths.TryFindPath(West, farAway, corners);

        found.Should().BeFalse();
    }

    [Fact]
    public void FindsNoWayWithoutAnyGround()
    {
        PathSystem paths = _builder.Build();
        List<Vector3> corners = [];

        bool found = paths.TryFindPath(West, East, corners);

        found.Should().BeFalse();
    }

    [Fact]
    public void RebuildsWhenStaticBodiesComeAndGo()
    {
        PathSystem paths = _builder.WithFloor().Build();
        List<Vector3> before = [];
        paths.TryFindPath(West, East, before);

        Entity wall = _builder.AddWall();
        List<Vector3> during = [];
        paths.TryFindPath(West, East, during);
        _builder.Remove(wall);
        List<Vector3> after = [];
        paths.TryFindPath(West, East, after);

        before.Should().ContainSingle();
        during.Count.Should().BeGreaterThan(1);
        after.Should().ContainSingle();
    }

    [Fact]
    public void WalksCharactersAroundObstacles()
    {
        _builder.WithFloor().WithWall().Build();
        Entity hero = _builder.AddWalker(West);

        _builder.Game.Events.Send(new MoveCommand(hero, East));
        _builder.Run(8);

        Vector3 position = _builder.Game.World.Get<Transform>(hero).Position;
        position.X.Should().BeApproximately(3, 0.1f);
        position.Z.Should().BeApproximately(0, 0.1f);
        _builder.MaxAbsZ.Should().BeGreaterThan(3, "it walked round the wall's end");
    }
}
