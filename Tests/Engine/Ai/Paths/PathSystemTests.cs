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
    public void FindsRandomPointsOnTheGroundWithinARadius()
    {
        PathSystem paths = _builder.WithFloor().WithWall().Build();
        Random random = new(7);
        List<Vector3> points = [];

        for (int index = 0; index < 20; index++)
        {
            paths.TryFindRandomPoint(West, 1.5f, random, out Vector3 point).Should().BeTrue();
            points.Add(point);
        }

        points.Should().AllSatisfy(point =>
        {
            Vector2 offset = new(point.X - West.X, point.Z - West.Z);
            offset.Length().Should().BeLessThanOrEqualTo(1.5f);
            point.Y.Should().BeApproximately(0, 0.2f);
        });
        points.Distinct().Should().HaveCountGreaterThan(10);
    }

    [Fact]
    public void FindsNoRandomPointWithoutAnyGround()
    {
        PathSystem paths = _builder.Build();

        paths.TryFindRandomPoint(West, 1.5f, new Random(7), out _).Should().BeFalse();
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
    public void GoesAroundAWallOfTrianglesAndThroughItsOpening()
    {
        PathSystem paths = _builder.WithFloor().Build();
        _builder.AddTriangleBox(new Vector3(0, 0, -3.5f), new Vector3(0.4f, 2, 3));
        _builder.AddTriangleBox(new Vector3(0, 0, 1.5f), new Vector3(0.4f, 2, 5));
        _builder.Run(0.1f);
        List<Vector3> corners = [];

        bool found = paths.TryFindPath(West, East, corners);

        found.Should().BeTrue();
        corners.Should().Contain(corner => MathF.Abs(corner.X) < 1 && corner.Z < -1 && corner.Z > -2, "the only way is the opening between z -2 and -1");
    }

    [Fact]
    public void WalksOnTopOfStandingTriangles()
    {
        PathSystem paths = _builder.Build();
        _builder.AddTriangleBox(new Vector3(0, -0.2f, 0), new Vector3(10, 0.2f, 10));
        _builder.Run(0.1f);
        List<Vector3> corners = [];

        paths.TryFindPath(West, East, corners).Should().BeTrue();
    }

    [Fact]
    public void GoesAroundAMovingPartsHull()
    {
        PathSystem paths = _builder.WithFloor().Build();
        _builder.AddHullBox(new Vector3(0, 0, 0), new Vector3(0.4f, 2, 6));
        _builder.Run(0.5f);
        List<Vector3> corners = [];

        paths.TryFindPath(West, East, corners).Should().BeTrue();

        corners.Should().Contain(corner => MathF.Abs(corner.Z) >= 3, "the way round passes the hull's end");
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
    public void GoesStraightAcrossTheTilesOfALongFloor()
    {
        PathSystem paths = _builder.WithLongFloor().Build();
        List<Vector3> corners = [];

        bool found = paths.TryFindPath(new Vector3(-15, 0, 0), new Vector3(15, 0, 0), corners);

        found.Should().BeTrue();
        corners.Should().ContainSingle();
        corners[0].X.Should().BeApproximately(15, 0.1f);
    }

    [Fact]
    public void GoesThroughBodiesThatAreNoObstacles()
    {
        PathSystem paths = _builder.WithFloor().Build();
        _builder.AddBody("Innkeeper", Vector3.Zero, """{ "kinematic": true, "collider": { "type": "box", "size": [0.6, 1.7, 0.4], "offset": [0, 0.85, 0] } }""");
        _builder.AddStaticBody("Key", new Vector3(1, 0, 0), """{ "obstacle": false, "collider": { "type": "box", "size": [2, 0.1, 6] } }""");
        List<Vector3> corners = [];

        paths.TryFindPath(West, East, corners);

        corners.Should().ContainSingle();
    }

    [Fact]
    public void GoesAroundAMovedObstacleOnceItComesToRest()
    {
        PathSystem paths = _builder.WithFloor().Build();
        Quaternion alongX = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Entity gate = _builder.AddBody("Gate", new Vector3(0, 0, 4), """{ "kinematic": true, "obstacle": true, "collider": { "type": "box", "size": [0.4, 2, 6], "offset": [0, 1, 0] } }""", alongX);
        _builder.Run(1 / 60f);
        List<Vector3> aside = [];
        paths.TryFindPath(West, East, aside);

        _builder.Place(gate, Vector3.Zero, Quaternion.Identity);
        _builder.Run(1 / 60f);
        List<Vector3> moving = [];
        paths.TryFindPath(West, East, moving);
        _builder.Run(1 / 60f);
        List<Vector3> resting = [];
        paths.TryFindPath(West, East, resting);

        aside.Should().ContainSingle();
        moving.Should().ContainSingle("paths wait until it stops moving");
        resting.Count.Should().BeGreaterThan(1);
        resting.Should().Contain(corner => MathF.Abs(corner.Z) >= 3, "the way round passes the gate's end");
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

    [Fact]
    public void KeepsSteeredCharactersOnTheGroundTheyCanWalk()
    {
        _builder.WithFloor().WithWall().Build();
        Entity hero = _builder.AddWalker(West);

        _builder.Game.Events.Send(new SteerCommand(hero, Vector3.UnitX));
        _builder.Run(4);

        Vector3 position = _builder.Game.World.Get<Transform>(hero).Position;
        position.X.Should().BeLessThan(-0.2f, "the wall stops it, as it stands outside the walkable ground");
        position.Y.Should().BeApproximately(0, 0.05f, "it stays on the floor rather than climbing the wall");
    }

    [Fact]
    public void ConstrainsAStepToTheWalkableGround()
    {
        PathSystem paths = _builder.WithFloor().WithWall().Build();

        Vector3 reached = paths.Constrain(new Vector3(-1, 0, 0), new Vector3(1, 0, 0));

        reached.X.Should().BeLessThan(-0.2f, "the step ends at the wall's side");
        reached.Z.Should().BeApproximately(0, 0.05f);
    }
}
