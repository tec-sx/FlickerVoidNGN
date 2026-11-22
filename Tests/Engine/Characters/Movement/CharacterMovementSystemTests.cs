using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;

using NSubstitute;

namespace FlickerVoid.Engine.Characters.Tests.Movement;

public sealed class CharacterMovementSystemTests : IDisposable
{
    private const string Walker = """{ "name": "Hero", "components": { "transform": {}, "characterMover": { "speed": 2 } } }""";

    private readonly CharacterMovementSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void WalksToTheDestinationAtItsSpeedAndSaysWhenItArrives()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        List<ArrivedEvent> arrived = [];
        EventBus events = _builder.Game.Events;
        events.Handle((in ArrivedEvent sent) => arrived.Add(sent));
        Vector3 door = new(4, 0, 0);
        events.Send(new MoveCommand(hero, door));

        _builder.Run(1);
        Vector3 halfway = _builder.Game.World.Get<Transform>(hero).Position;
        _builder.Run(1.2f);

        halfway.X.Should().BeApproximately(2, 0.1f);
        Transform transform = _builder.Game.World.Get<Transform>(hero);
        transform.Position.X.Should().BeApproximately(4, 0.06f);
        arrived.Should().ContainSingle().Which.Character.Should().Be(hero);
        _builder.Game.World.Has<Destination>(hero).Should().BeFalse();
    }

    [Fact]
    public void ASavedCharacterWalksOnWhereItWasGoingAfterALoad()
    {
        _builder.Build();
        Entity hero = _builder.Spawn("""{ "name": "Hero", "components": { "transform": {}, "characterMover": { "speed": 2 }, "saveable": { "id": "Hero" } } }""")[0];
        _builder.Game.Lifecycle.Play(_builder.Game.World);
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(4, 0, 0)));
        _builder.Run(1);
        _builder.Saves.Save("Slot1");
        _builder.Game.Events.Send(new StopCommand(hero));
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(-4, 0, 0)));
        _builder.Run(0.5f);

        _builder.Saves.Load("Slot1");
        Vector3 loaded = _builder.Game.World.Get<Transform>(hero).Position;
        _builder.Run(1.2f);

        loaded.X.Should().BeApproximately(2, 0.1f);
        _builder.Game.World.Get<Transform>(hero).Position.X.Should().BeApproximately(4, 0.06f);
    }

    [Fact]
    public void TurnsToFaceWhereItWalks()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        Vector3 east = new(10, 0, 0);
        _builder.Game.Events.Send(new MoveCommand(hero, east));

        _builder.Run(0.5f);

        Quaternion rotation = _builder.Game.World.Get<Transform>(hero).Rotation;
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, rotation);
        forward.X.Should().BeApproximately(1, 1e-3f);
    }

    [Fact]
    public void StopsWhereItStands()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(10, 0, 0)));
        _builder.Run(0.5f);

        _builder.Game.Events.Send(new StopCommand(hero));
        _builder.Run(0.1f);
        float stopped = _builder.Game.World.Get<Transform>(hero).Position.X;
        _builder.Run(1);

        _builder.Game.World.Get<Transform>(hero).Position.X.Should().Be(stopped);
    }

    [Fact]
    public void TurnsInPlaceToFaceAPointAtItsTurnSpeed()
    {
        _builder.Build();
        Entity hero = _builder.Spawn("""{ "name": "Hero", "components": { "transform": {}, "characterMover": { "turnSpeed": 90 } } }""")[0];
        _builder.Game.Events.Send(new FaceCommand(hero, new Vector3(-3, 0, 0)));

        _builder.Run(0.5f);
        Vector3 halfway = Vector3.Transform(Vector3.UnitZ, _builder.Game.World.Get<Transform>(hero).Rotation);
        _builder.Run(0.6f);

        halfway.X.Should().BeApproximately(-MathF.Sin(MathF.PI / 4), 0.03f);
        Transform transform = _builder.Game.World.Get<Transform>(hero);
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, transform.Rotation);
        forward.X.Should().BeApproximately(-1, 1e-3f);
        transform.Position.Should().Be(Vector3.Zero);
        _builder.Game.World.Has<Facing>(hero).Should().BeFalse();
    }

    [Fact]
    public void FacingAPointStopsItWalking()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(10, 0, 0)));
        _builder.Run(0.5f);

        _builder.Game.Events.Send(new FaceCommand(hero, new Vector3(0, 0, 10)));
        _builder.Run(0.1f);
        float stopped = _builder.Game.World.Get<Transform>(hero).Position.X;
        _builder.Run(1);

        _builder.Game.World.Get<Transform>(hero).Position.X.Should().Be(stopped);
    }

    [Fact]
    public void StandsOnTheGroundPhysicsFinds()
    {
        _builder.WithPhysics().Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(Walker + """
            , { "name": "Step", "components": {
                "transform": { "position": [3, 0.1, 0] },
                "staticBody": { "collider": { "type": "box", "size": [2, 0.2, 2] } } } }
            """);
        _builder.Game.Events.Send(new MoveCommand(entities[0], new Vector3(3, 0, 0)));

        _builder.Run(2);

        _builder.Game.World.Get<Transform>(entities[0]).Position.Y.Should().BeApproximately(0.2f, 1e-3f);
    }

    [Fact]
    public void FollowsThePathItsPathFinderGivesCornerByCorner()
    {
        IPathFinder paths = Substitute.For<IPathFinder>();
        Vector3 door = new(4, 0, 0);
        paths.TryFindPath(Arg.Any<Vector3>(), door, Arg.Any<List<Vector3>>()).Returns(call =>
        {
            List<Vector3> corners = call.ArgAt<List<Vector3>>(2);
            corners.Add(new Vector3(0, 0, 2));
            corners.Add(new Vector3(4, 0, 2));
            return true;
        });
        _builder.WithPathFinder(paths).Build();
        Entity hero = _builder.Spawn(Walker)[0];
        List<ArrivedEvent> arrived = [];
        _builder.Game.Events.Handle((in ArrivedEvent sent) => arrived.Add(sent));
        _builder.Game.Events.Send(new MoveCommand(hero, door));

        _builder.Run(1);
        Vector3 atCorner = _builder.Game.World.Get<Transform>(hero).Position;
        _builder.Run(2.2f);

        atCorner.X.Should().BeApproximately(0, 0.06f);
        atCorner.Z.Should().BeApproximately(2, 0.1f);
        Vector3 end = _builder.Game.World.Get<Transform>(hero).Position;
        end.X.Should().BeApproximately(4, 0.06f);
        end.Z.Should().BeApproximately(2, 0.06f);
        arrived.Should().ContainSingle().Which.Point.Should().Be(new Vector3(4, 0, 2));
    }

    [Fact]
    public void StaysWhereItIsWhenThePathFinderFindsNoWay()
    {
        IPathFinder paths = Substitute.For<IPathFinder>();
        _builder.WithPathFinder(paths).Build();
        Entity hero = _builder.Spawn(Walker)[0];
        _builder.Game.Events.Send(new MoveCommand(hero, new Vector3(4, 0, 0)));

        _builder.Run(1);

        _builder.Game.World.Get<Transform>(hero).Position.Should().Be(Vector3.Zero);
        _builder.Game.World.Has<Destination>(hero).Should().BeFalse();
    }

    [Fact]
    public void WalksAlongASteeredDirectionUntilSteeredToStandStill()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        List<ArrivedEvent> arrived = [];
        EventBus events = _builder.Game.Events;
        events.Handle((in ArrivedEvent sent) => arrived.Add(sent));
        events.Send(new SteerCommand(hero, new Vector3(0, 0, -1)));

        _builder.Run(1);
        events.Send(new SteerCommand(hero, Vector3.Zero));
        _builder.Run(0.1f);
        Vector3 stopped = _builder.Game.World.Get<Transform>(hero).Position;
        _builder.Run(1);

        stopped.Z.Should().BeApproximately(-2, 0.1f);
        _builder.Game.World.Get<Transform>(hero).Position.Should().Be(stopped);
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, _builder.Game.World.Get<Transform>(hero).Rotation);
        forward.Z.Should().BeApproximately(-1, 1e-3f);
        arrived.Should().BeEmpty();
        _builder.Game.World.Has<Heading>(hero).Should().BeFalse();
    }

    [Fact]
    public void SteeringReplacesTheDestinationAndAMoveReplacesTheSteering()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        EventBus events = _builder.Game.Events;
        events.Send(new MoveCommand(hero, new Vector3(10, 0, 0)));
        _builder.Run(0.1f);

        events.Send(new SteerCommand(hero, new Vector3(0, 0, 1)));
        _builder.Run(0.1f);
        bool destinationAfterSteer = _builder.Game.World.Has<Destination>(hero);
        events.Send(new MoveCommand(hero, new Vector3(0, 0, 5)));
        _builder.Run(0.1f);

        destinationAfterSteer.Should().BeFalse();
        _builder.Game.World.Has<Heading>(hero).Should().BeFalse();
        _builder.Game.World.Has<Destination>(hero).Should().BeTrue();
    }

    [Fact]
    public void AStopEndsTheSteering()
    {
        _builder.Build();
        Entity hero = _builder.Spawn(Walker)[0];
        _builder.Game.Events.Send(new SteerCommand(hero, new Vector3(1, 0, 0)));
        _builder.Run(0.1f);

        _builder.Game.Events.Send(new StopCommand(hero));
        _builder.Run(0.1f);

        _builder.Game.World.Has<Heading>(hero).Should().BeFalse();
    }
}
