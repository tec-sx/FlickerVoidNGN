using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

/// <summary>Builds the view under test on a new entity in the given world.</summary>
internal sealed class WorldEntityViewTestsBuilder(World world)
{
    private string? _name;

    public WorldEntityViewTestsBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorldEntityView Build()
    {
        Entity entity = world.Create();
        if (_name is not null)
        {
            EntityName name = new(_name);
            world.Add(entity, in name);
        }

        return new WorldEntityView(world, entity);
    }
}
