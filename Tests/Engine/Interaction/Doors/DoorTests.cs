using FlickerVoid.Engine.Interaction.Locks;
using FlickerVoid.Engine.Interaction.Toggles;

namespace FlickerVoid.Engine.Interaction.Tests.Doors;

public sealed class DoorTests : IDisposable
{
    private readonly DoorTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ATogglesActionOpensAndClosesIt()
    {
        _builder.WithDoor("""
            "toggle": { "action": "Interaction.Action.Toggle" }
            """).Build();
        List<ToggledEvent> toggled = _builder.Collect<ToggledEvent>();

        _builder.Press("Input.Use");
        _builder.Press("Input.Use");

        toggled.Select(sent => sent.Open).Should().Equal(true, false);
        _builder.World.Get<Toggle>(_builder.Door).Open.Should().BeFalse();
    }

    [Fact]
    public void ALockedToggleSaysItIsBlockedAndStaysShut()
    {
        _builder.Build();
        List<ToggleBlockedEvent> blocked = _builder.Collect<ToggleBlockedEvent>();

        _builder.Press("Input.Use");

        blocked.Should().ContainSingle().Which.Should().Be(new ToggleBlockedEvent(_builder.Door, _builder.Hero, Opening: true));
        _builder.World.Get<Toggle>(_builder.Door).Open.Should().BeFalse();
    }

    [Fact]
    public void ALocksActionUnlocksItAndThenTheToggleOpens()
    {
        _builder.Build();
        List<LockChangedEvent> changed = _builder.Collect<LockChangedEvent>();

        _builder.Press("Input.Unlock");
        _builder.Press("Input.Use");

        changed.Should().ContainSingle().Which.Should().Be(new LockChangedEvent(_builder.Door, Locked: false, _builder.Hero));
        _builder.World.Get<Toggle>(_builder.Door).Open.Should().BeTrue();
    }

    [Fact]
    public void TheUnlockOfferNeedsTheLockLocked()
    {
        _builder.WithDoor("""
            "toggle": { "action": "Interaction.Action.Toggle" },
            "lock": { "locked": false, "action": "Interaction.Action.Unlock" }
            """).Build();
        List<LockChangedEvent> changed = _builder.Collect<LockChangedEvent>();

        _builder.Press("Input.Unlock");

        changed.Should().BeEmpty();
    }
}
