using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

/// <summary>Fame as onlookers read it on the Instigator: a disguise hides most of it. For the real fame use a scale condition on the fame scale. At-least comparisons are scored.</summary>
public sealed class RecognizedFameCondition : Condition
{
    public Compare Compare { get; init; } = Compare.GreaterOrEqual;

    public int Value { get; init; }

    public override string Describe()
    {
        string symbol = Compare.ToSymbol();
        return $"Recognized fame {symbol} {Value}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        ISocialRules? rules = context.GetService<ISocialRules>();
        if (rules is null)
        {
            return false;
        }

        int fame = rules.GetRecognizedFame(context.Instigator);
        return Compare.Test(fame, Value);
    }

    protected override bool TryScore(ConditionContext context, out ConditionScore score)
    {
        ISocialRules? rules = context.GetService<ISocialRules>();
        if (rules is null)
        {
            score = default;
            return false;
        }

        int fame = rules.GetRecognizedFame(context.Instigator);
        return Compare.TryScore(fame, Value, out score);
    }
}
