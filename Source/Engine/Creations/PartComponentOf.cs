using Arch.Core;

namespace FlickerVoid.Engine.Creations;

/// <summary>The <see cref="PartComponent"/> of a component type, which reaches the world through Arch's typed API.</summary>
internal sealed class PartComponentOf<T> : PartComponent
    where T : struct
{
    private readonly T _value;

    public PartComponentOf(in T value)
    {
        _value = value;
    }

    public override Type Type => typeof(T);

    public override void Set(World world, Entity entity)
    {
        if (world.Has<T>(entity))
        {
            world.Set(entity, in _value);
        }
        else
        {
            world.Add(entity, in _value);
        }
    }

    public override void Remove(World world, Entity entity)
    {
        if (world.Has<T>(entity))
        {
            world.Remove<T>(entity);
        }
    }

    public override bool Matches(PartComponent other)
    {
        return other is PartComponentOf<T> same && EqualityComparer<T>.Default.Equals(_value, same._value);
    }
}
