using FlickerVoid.Engine.Gameplay.Conditions;

namespace FlickerVoid.Engine.Gameplay.Scales;

/// <summary>Compares a scale's value; at-least comparisons are scored, so the hardest threshold ranks first.</summary>
public sealed class ScaleCondition : Condition
{
    public required ScaleDefinition Scale { get; init; }

    public Compare Compare { get; init; } = Compare.GreaterOrEqual;

    public int Value { get; init; }

    public override string Describe()
    {
        string symbol = Compare.ToSymbol();
        return $"{Scale.Display.Name} {symbol} {Value}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        int current = Scale.GetValue(context.Facts);
        return Compare.Test(current, Value);
    }

    protected override bool TryScore(ConditionContext context, out ConditionScore score)
    {
        int current = Scale.GetValue(context.Facts);
        return Compare.TryScore(current, Value, out score);
    }
}
