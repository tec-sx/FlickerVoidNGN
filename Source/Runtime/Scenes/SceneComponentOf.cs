using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>The <see cref="SceneComponent"/> of a component type, which reaches the world through Arch's typed API.</summary>
internal sealed class SceneComponentOf<T> : SceneComponent
    where T : struct
{
    private readonly JsonTypeInfo<T> _typeInfo;

    public SceneComponentOf(string name, JsonTypeInfo<T> typeInfo)
        : base(name, typeInfo)
    {
        _typeInfo = typeInfo;
    }

    public override bool Has(World world, Entity entity)
    {
        return world.Has<T>(entity);
    }

    public override object Get(World world, Entity entity)
    {
        return world.Get<T>(entity);
    }

    public override void Set(World world, Entity entity, object component)
    {
        Set(world, entity, (T)component);
    }

    public override void Read(World world, Entity entity, JsonElement data)
    {
        T component = data.Deserialize(_typeInfo);
        Set(world, entity, component);
    }

    public override void Remove(World world, Entity entity)
    {
        if (world.Has<T>(entity))
        {
            world.Remove<T>(entity);
        }
    }

    private static void Set(World world, Entity entity, T component)
    {
        if (world.Has<T>(entity))
        {
            world.Set(entity, in component);
        }
        else
        {
            world.Add(entity, in component);
        }
    }
}
