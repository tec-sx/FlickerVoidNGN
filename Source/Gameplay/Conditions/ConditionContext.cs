using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>What conditions are checked against and effects applied to: the facts, the entities involved and the game's services.</summary>
public readonly record struct ConditionContext(IFactDatabase Facts)
{
    /// <summary>Who acts, usually the player.</summary>
    public IEntityView? Instigator { get; init; }

    /// <summary>Who or what is acted on, usually the character the player talks to.</summary>
    public IEntityView? Target { get; init; }

    public IServiceProvider? Services { get; init; }

    public IEntityView? GetEntity(ContextEntity which)
    {
        if (which == ContextEntity.Target)
        {
            return Target;
        }

        return Instigator;
    }

    /// <summary>The service registered under <typeparamref name="T"/>; null when there are no services or none of that type.</summary>
    public T? GetService<T>()
        where T : class
    {
        object? service = Services?.GetService(typeof(T));
        return service as T;
    }
}
