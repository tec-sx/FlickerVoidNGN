using System.Diagnostics.CodeAnalysis;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Entities;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>An entity in a world, as the gameplay rules see it.</summary>
public readonly record struct WorldEntityView(World World, Entity Entity) : IEntityView
{
    public bool TryGet<T>([MaybeNullWhen(false)] out T component)
    {
        if (!World.IsAlive(Entity))
        {
            component = default;
            return false;
        }

        return World.TryGet(Entity, out component);
    }
}
