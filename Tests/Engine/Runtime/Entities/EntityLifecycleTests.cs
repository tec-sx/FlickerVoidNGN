using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

public sealed class EntityLifecycleTests : IDisposable
{
    private readonly EntityLifecycleTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AttachesThenActivatesInTheOrderHooksWereAddedWhilePlaying()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform(), new EntityName("Door"));

        lifecycle.Start(_builder.World, entity);

        _builder.Calls.Should().Equal("attach Transform", "attach EntityName", "activate Transform", "activate EntityName");
    }

    [Fact]
    public void OnlyAttachesWhenNotPlaying()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Build();
        Entity entity = _builder.CreateEntity(new Transform(), new EntityName("Door"));

        lifecycle.Start(_builder.World, entity);

        _builder.Calls.Should().Equal("attach Transform", "attach EntityName");
    }

    [Fact]
    public void RunsOnlyTheHooksOfComponentsTheEntityHas()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform());

        lifecycle.Start(_builder.World, entity);

        _builder.Calls.Should().Equal("attach Transform", "activate Transform");
    }

    [Fact]
    public void StartsAnEntityOnce()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform());

        lifecycle.Start(_builder.World, entity);
        lifecycle.Start(_builder.World, entity);

        _builder.Calls.Should().Equal("attach Transform", "activate Transform");
    }

    [Fact]
    public void StopsInReverseOrder()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform(), new EntityName("Door"));
        lifecycle.Start(_builder.World, entity);
        _builder.Calls.Clear();

        lifecycle.Stop(_builder.World, entity);

        _builder.Calls.Should().Equal("deactivate EntityName", "deactivate Transform", "detach EntityName", "detach Transform");
    }

    [Fact]
    public void DestroysAnEntityAfterStoppingIt()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Build();
        Entity entity = _builder.CreateEntity(new Transform());
        lifecycle.Start(_builder.World, entity);
        _builder.Calls.Clear();

        lifecycle.Destroy(_builder.World, entity);

        _builder.Calls.Should().Equal("detach Transform");
        _builder.World.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void StopsEveryEntityWhenAHookDestroysOthers()
    {
        EntityLifecycle lifecycle = _builder.Playing().Build();
        Entity part = _builder.CreateEntity(new Transform());
        Entity van = _builder.CreateEntity(new EntityName("Van"));
        lifecycle.OnDetach((Entity entity, ref EntityName name) => lifecycle.Destroy(_builder.World, part));
        lifecycle.Start(_builder.World, part);
        lifecycle.Start(_builder.World, van);

        Action stop = () => lifecycle.StopAll(_builder.World);

        stop.Should().NotThrow();
        _builder.World.IsAlive(part).Should().BeFalse();
    }

    [Fact]
    public void ActivatesTheAttachedEntitiesWhenPlayStarts()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Build();
        Entity entity = _builder.CreateEntity(new Transform());
        lifecycle.Start(_builder.World, entity);
        _builder.Calls.Clear();

        lifecycle.Play(_builder.World);

        lifecycle.IsPlaying.Should().BeTrue();
        _builder.Calls.Should().Equal("activate Transform");
    }

    [Fact]
    public void RestartsOneComponentAroundAChange()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform(), new EntityName("Door"));
        lifecycle.Start(_builder.World, entity);
        _builder.Calls.Clear();

        lifecycle.StopComponent(_builder.World, entity, typeof(EntityName));
        lifecycle.StartComponent(_builder.World, entity, typeof(EntityName));

        _builder.Calls.Should().Equal("deactivate EntityName", "detach EntityName", "attach EntityName", "activate EntityName");
    }

    [Fact]
    public void LeavesAComponentOfAnUnstartedEntityAlone()
    {
        EntityLifecycle lifecycle = _builder.WithRecordingHooks().Playing().Build();
        Entity entity = _builder.CreateEntity(new Transform());

        lifecycle.StartComponent(_builder.World, entity, typeof(Transform));
        lifecycle.StopComponent(_builder.World, entity, typeof(Transform));

        _builder.Calls.Should().BeEmpty();
    }

    [Fact]
    public void LetsHooksChangeTheComponent()
    {
        EntityLifecycle lifecycle = _builder.Build();
        lifecycle.OnAttach((Entity _, ref EntityName name) => name = new EntityName(name.Value + " (attached)"));
        Entity entity = _builder.CreateEntity(new EntityName("Door"));

        lifecycle.Start(_builder.World, entity);

        _builder.World.Get<EntityName>(entity).Value.Should().Be("Door (attached)");
    }
}
