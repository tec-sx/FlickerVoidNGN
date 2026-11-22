using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Tests.Triggers;

public sealed class TriggerAreaSystemTests : IDisposable
{
    private readonly TriggerAreaSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SendsEnteredAndExitedOnceAsAnActivatorWalksThrough()
    {
        _builder.WithArea();
        Entity fox = _builder.SpawnActivator(new Vector3(-3, 0, 0));
        List<AreaEnteredEvent> entered = _builder.Game.Collect<AreaEnteredEvent>();
        List<AreaExitedEvent> exited = _builder.Game.Collect<AreaExitedEvent>();

        for (float x = -3; x <= 5; x += 0.25f)
        {
            _builder.Game.MoveTo(fox, new Vector3(x, 0, 0));
            _builder.Game.Run(1 / 60f);
        }

        entered.Should().ContainSingle().Which.Should().Be(new AreaEnteredEvent(_builder.Area, fox));
        exited.Should().ContainSingle().Which.Should().Be(new AreaExitedEvent(_builder.Area, fox));
    }

    [Fact]
    public void PlacesItsBoxByItsOffsetInItsEntitysSpace()
    {
        _builder.WithArea();
        Entity inside = _builder.SpawnActivator(new Vector3(1.9f, 0, 0));
        _builder.SpawnActivator(new Vector3(-0.1f, 0, 0));
        List<AreaEnteredEvent> entered = _builder.Game.Collect<AreaEnteredEvent>();

        _builder.Game.Run(1 / 60f);

        entered.Should().ContainSingle().Which.Activator.Should().Be(inside);
    }

    [Fact]
    public void IgnoresEntitiesThatAreNotActivators()
    {
        _builder.WithArea();
        _builder.SpawnActivator(new Vector3(1, 0, 0), activator: false);
        List<AreaEnteredEvent> entered = _builder.Game.Collect<AreaEnteredEvent>();

        _builder.Game.Run(1 / 60f);

        entered.Should().BeEmpty();
    }

    [Fact]
    public void OnlyActivatorsWithTheRequiredTagsSetItOff()
    {
        _builder.WithArea(""" "Character.Protagonist" """);
        Entity fox = _builder.SpawnActivator(new Vector3(1, 0, 0));
        List<AreaEnteredEvent> entered = _builder.Game.Collect<AreaEnteredEvent>();
        _builder.Game.Run(1 / 60f);
        bool before = entered.Count > 0;

        _builder.Game.Game.World.Add(fox, new OwnedTags([new Tag("Character.Protagonist")]));
        _builder.Game.Run(1 / 60f);

        before.Should().BeFalse();
        entered.Should().ContainSingle();
    }

    [Fact]
    public void ADestroyedActivatorLeavesTheArea()
    {
        _builder.WithArea();
        Entity fox = _builder.SpawnActivator(new Vector3(1, 0, 0));
        _builder.Game.Run(1 / 60f);
        List<AreaExitedEvent> exited = _builder.Game.Collect<AreaExitedEvent>();

        _builder.Game.Game.Lifecycle.Destroy(_builder.Game.Game.World, fox);
        _builder.Game.Run(1 / 60f);

        exited.Should().ContainSingle();
    }
}
