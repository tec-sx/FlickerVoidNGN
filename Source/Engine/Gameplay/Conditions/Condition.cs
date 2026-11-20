namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>A rule checked against a context; shared by everything that uses its asset, so it reads only its properties and the context.</summary>
public abstract class Condition
{
    public bool Invert { get; set; }

    /// <summary>Evaluate's result, flipped when Invert is set.</summary>
    public bool Check(ConditionContext context) => Evaluate(context) != Invert;

    /// <summary>Ranks what an at-least condition (fame >= 80) unlocks; false when the condition isn't scored or is inverted.</summary>
    public bool TryGetScore(ConditionContext context, out ConditionScore score)
    {
        if (Invert)
        {
            score = default;
            return false;
        }

        return TryScore(context, out score);
    }

    public virtual string Describe() => GetType().Name;

    /// <summary>Whether the condition holds, before Invert.</summary>
    protected abstract bool Evaluate(ConditionContext context);

    /// <summary>Override for at-least conditions: fill the score and return true.</summary>
    protected virtual bool TryScore(ConditionContext context, out ConditionScore score)
    {
        score = default;
        return false;
    }
}
