using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Ai.Senses;
using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Dialogue.Conversations;

namespace FlickerVoid.Gameplay.Tests.World.Society.Residents;

public sealed class ResidentSystemTests : IDisposable
{
    private readonly ResidentSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void WandersAboutItsHomeAndRestsBetweenWalks()
    {
        _builder.Build(wanderRadius: 2, rest: 0.5f);

        _builder.Run(8);

        _builder.Positions.Should().AllSatisfy(position => new Vector2(position.X, position.Z).Length().Should().BeLessThan(2.1f));
        List<Vector3> places = _builder.Positions.Distinct().ToList();
        places.Should().Contain(place => new Vector2(place.X, place.Z).Length() > 0.5f);
        int still = _builder.Positions.Zip(_builder.Positions.Skip(1)).Count(pair => pair.First == pair.Second);
        still.Should().BeGreaterThan(30, "it rests where it arrives before walking on");
    }

    [Fact]
    public void StaysAtHomeWithoutAWanderRadius()
    {
        _builder.Build(wanderRadius: 0);

        _builder.Run(4);

        _builder.Positions.Should().AllSatisfy(position => position.Should().Be(Vector3.Zero));
    }

    [Fact]
    public void StopsWanderingToWatchWhoItSees()
    {
        _builder.Build(wanderRadius: 2, rest: 0.2f);
        _builder.Run(0.5f);
        Entity fox = _builder.AddFox(new Vector3(2, 0, 3));

        _builder.Run(0.5f);
        Vector3 watching = _builder.Positions[^1];
        _builder.Run(2);

        _builder.Positions[^1].Should().Be(watching);
        Vector3 toFox = Vector3.Normalize(new Vector3(2 - watching.X, 0, 3 - watching.Z));
        Vector3.Dot(_builder.Forward(), toFox).Should().BeGreaterThan(0.999f);
        PerceptionSystem perception = _builder.Game.Services.GetService<PerceptionSystem>()!;
        perception.GetSeen(_builder.Resident).Should().Contain(fox);
    }

    [Fact]
    public void TurnsTowardANoiseItHears()
    {
        _builder.Build(wanderRadius: 0);
        Entity fox = _builder.AddFox(new Vector3(0, 0, -9));

        _builder.Game.Events.Send(new NoiseEvent(fox, new Vector3(0, 0, -9)));
        _builder.Run(1);

        _builder.Forward().Z.Should().BeApproximately(-1, 1e-3f);
    }

    [Fact]
    public void FacesWhoItTalksTo()
    {
        _builder.Build(wanderRadius: 2, rest: 0.2f);
        Entity fox = _builder.Game.World.Create(new Engine.Runtime.Components.Transform { Position = new Vector3(-3, 0, -3) });
        DialogueSystem dialogue = _builder.Game.Services.GetService<DialogueSystem>()!;

        dialogue.Start(fox, _builder.Resident).Should().BeTrue();
        _builder.Run(2);

        Vector3 here = _builder.Positions[^1];
        Vector3 toFox = Vector3.Normalize(new Vector3(-3 - here.X, 0, -3 - here.Z));
        Vector3.Dot(_builder.Forward(), toFox).Should().BeGreaterThan(0.999f);
        _builder.Game.World.Has<Destination>(_builder.Resident).Should().BeFalse();
    }
}
