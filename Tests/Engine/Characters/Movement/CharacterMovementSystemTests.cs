using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Events;

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
}
