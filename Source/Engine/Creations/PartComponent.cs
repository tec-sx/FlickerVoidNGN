using Arch.Core;

namespace FlickerVoid.Engine.Creations;

/// <summary>A component a kind gives one of its parts, which reaches the world without the parts code knowing its type.</summary>
internal abstract class PartComponent
{
    public abstract Type Type { get; }

    /// <summary>Gives the entity the component, replacing the one it has.</summary>
    public abstract void Set(World world, Entity entity);

    public abstract void Remove(World world, Entity entity);

    /// <summary>Whether the other is a component of the same type with the same value.</summary>
    public abstract bool Matches(PartComponent other);
}
