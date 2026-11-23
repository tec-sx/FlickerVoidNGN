using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>A typed handle over an entity of a class, its components reached as properties; written by the generator for each <see cref="EntityClassAttribute"/>.</summary>
public interface IEntityClass<TSelf>
    where TSelf : struct, IEntityClass<TSelf>
{
    /// <summary>The component types every entity of the class has, in declared order.</summary>
    static abstract IReadOnlyList<Type> Components { get; }

    /// <summary>The entity template the class spawns from, or null to spawn its components at their defaults.</summary>
    static abstract string? Template { get; }

    /// <summary>Every entity with the class's components.</summary>
    static abstract QueryDescription Query { get; }

    World World { get; }

    Entity Entity { get; }

    /// <summary>Whether the entity has every component of the class.</summary>
    static abstract bool Is(World world, Entity entity);

    /// <summary>The handle over an entity known to have every component of the class.</summary>
    static abstract TSelf From(World world, Entity entity);
}
