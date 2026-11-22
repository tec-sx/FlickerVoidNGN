using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Gameplay.World.Objects.Doors;

namespace FlickerVoid.Gameplay.Tests.World.Objects.Doors;

public sealed class LockpickSystemTests : IDisposable
{
    private readonly LockpickSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AHeroWithALockpickPicksTheLockAndTheDoorOpens()
    {
        _builder.Build(perception: 10, lockpick: true);
        List<LockpickedEvent> picked = _builder.Collect<LockpickedEvent>();

        _builder.TryOpen();

        LockpickedEvent attempt = picked.Should().ContainSingle().Subject;
        attempt.Result.Success.Should().BeTrue();
        attempt.Result.Difficulty.Should().Be(9);
        _builder.Game.World.Get<Lockable>(_builder.Door).Locked.Should().BeFalse();
        _builder.Game.World.Get<Toggle>(_builder.Door).Open.Should().BeTrue();
    }

    [Fact]
    public void AFailedPickLeavesTheDoorLockedAndTheAbilityCancelled()
    {
        _builder.Build(perception: 0, lockpick: true, difficulty: 1);
        List<LockpickedEvent> picked = _builder.Collect<LockpickedEvent>();
        List<AbilityEndedEvent> ended = _builder.Collect<AbilityEndedEvent>();

        _builder.TryOpen();

        picked.Should().ContainSingle().Which.Result.Success.Should().BeFalse();
        ended.Should().ContainSingle().Which.Cancelled.Should().BeTrue();
        _builder.Game.World.Get<Lockable>(_builder.Door).Locked.Should().BeTrue();
        _builder.Game.World.Get<Toggle>(_builder.Door).Open.Should().BeFalse();
    }

    [Fact]
    public void WithoutALockpickNothingIsTried()
    {
        _builder.Build(perception: 10, lockpick: false);
        List<LockpickedEvent> picked = _builder.Collect<LockpickedEvent>();
        List<AbilityFailedEvent> failed = _builder.Collect<AbilityFailedEvent>();

        _builder.TryOpen();

        picked.Should().BeEmpty();
        failed.Should().BeEmpty();
        _builder.Game.World.Get<Lockable>(_builder.Door).Locked.Should().BeTrue();
    }
}
