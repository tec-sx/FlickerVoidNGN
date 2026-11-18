namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>Effects applied in order.</summary>
public sealed class EffectList
{
    public List<Effect> Effects { get; init; } = [];

    public bool IsEmpty => Effects.Count == 0;

    public void Apply(ConditionContext context)
    {
        foreach (Effect effect in Effects)
        {
            effect.Apply(context);
        }
    }

    public string Describe()
    {
        IEnumerable<string> parts = Effects.Select(static effect => effect.Describe());
        return string.Join(", ", parts);
    }
}
