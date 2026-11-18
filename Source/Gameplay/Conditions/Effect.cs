namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>A change applied with a context; shared by everything that uses its asset, so it writes its result to the world and never to itself.</summary>
public abstract class Effect
{
    public abstract void Apply(ConditionContext context);

    public virtual string Describe() => GetType().Name;
}
