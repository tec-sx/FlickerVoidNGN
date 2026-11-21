using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Compares a fact, or checks whether it is defined.</summary>
public sealed class FactCondition : Condition
{
    public Tag Fact { get; set; }

    public FactCompare Compare { get; set; } = FactCompare.GreaterOrEqual;

    /// <summary>Unused by Defined and Undefined.</summary>
    public int Value { get; set; } = 1;

    public override string Describe()
    {
        string symbol = Compare.ToSymbol();
        if (Compare == FactCompare.Defined || Compare == FactCompare.Undefined)
        {
            return $"{Fact} {symbol}";
        }

        return $"{Fact} {symbol} {Value}";
    }

    protected override bool Evaluate(ConditionContext context) => Compare.Test(context.Facts, Fact, Value);

    protected override bool TryScore(ConditionContext context, out ConditionScore score)
    {
        int current = context.Facts.GetFact(Fact);
        return Compare.TryScore(current, Value, out score);
    }
}
