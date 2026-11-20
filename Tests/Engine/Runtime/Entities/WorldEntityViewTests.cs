using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

public sealed class WorldEntityViewTests : IDisposable
{
    private readonly World _world = World.Create();
    private readonly WorldEntityViewTestsBuilder _builder;

    public WorldEntityViewTests()
    {
        _builder = new WorldEntityViewTestsBuilder(_world);
    }

    public void Dispose()
    {
        World.Destroy(_world);
    }

    [Fact]
    public void ReadsTheEntitysComponents()
    {
        WorldEntityView view = _builder.WithName("Guard").Build();

        bool found = view.TryGet(out EntityName name);

        found.Should().BeTrue();
        name.Value.Should().Be("Guard");
    }

    [Fact]
    public void FindsNothingTheEntityLacks()
    {
        WorldEntityView view = _builder.Build();

        bool found = view.TryGet(out EntityName _);

        found.Should().BeFalse();
    }

    [Fact]
    public void FindsNothingOnceTheEntityIsDestroyed()
    {
        WorldEntityView view = _builder.WithName("Guard").Build();
        _world.Destroy(view.Entity);

        bool found = view.TryGet(out EntityName _);

        found.Should().BeFalse();
    }
}
