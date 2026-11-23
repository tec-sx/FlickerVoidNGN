using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

public sealed class EntityClassTests : IDisposable
{
    private readonly World _world = World.Create();

    public void Dispose()
    {
        World.Destroy(_world);
    }

    [Fact]
    public void ReachesTheEntitysOwnComponentsThroughItsProperties()
    {
        Entity entity = _world.Create(new Transform(), new Glow());
        Lamp lamp = new(_world, entity);

        lamp.Glow.Intensity = 3;
        lamp.Transform.Position = new Vector3(1, 2, 3);

        _world.Get<Glow>(entity).Intensity.Should().Be(3);
        _world.Get<Transform>(entity).Position.Should().Be(new Vector3(1, 2, 3));
        lamp.Entity.Should().Be(entity);
    }

    [Fact]
    public void IsAnEntityOnlyWhenItHasEveryComponentOfTheClass()
    {
        Entity lampEntity = _world.Create(new Transform(), new Glow());
        Entity glowOnly = _world.Create(new Glow());

        Lamp.TryGet(_world, lampEntity, out Lamp lamp).Should().BeTrue();
        lamp.Entity.Should().Be(lampEntity);
        Lamp.TryGet(_world, glowOnly, out _).Should().BeFalse();
        Lantern.Is(_world, glowOnly).Should().BeTrue();
    }

    [Fact]
    public void IsNoEntityOnceTheEntityIsDestroyed()
    {
        Entity entity = _world.Create(new Transform(), new Glow());
        _world.Destroy(entity);

        Lamp.Is(_world, entity).Should().BeFalse();
    }

    [Fact]
    public void QueriesEveryEntityOfTheClass()
    {
        _world.Create(new Transform(), new Glow());
        _world.Create(new Transform(), new Glow());
        _world.Create(new Glow());

        int lamps = _world.CountEntities(Lamp.Query);

        lamps.Should().Be(2);
        Lamp.Components.Should().Equal(typeof(Transform), typeof(Glow));
        Lamp.Template.Should().BeNull();
        Lantern.Template.Should().Be("Props/Lantern.fventity");
    }
}
