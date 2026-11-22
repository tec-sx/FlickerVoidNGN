using Arch.Core;

using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Rules;

/// <summary>Compares an attribute's current value of the subject, the instigator by default; at-least comparisons are scored. Fails when the subject lacks it.</summary>
public sealed class AttributeCondition : Condition
{
    public Tag Attribute { get; set; }

    public Compare Compare { get; set; } = Compare.GreaterOrEqual;

    public float Value { get; set; }

    public ContextEntity Subject { get; set; }

    public override string Describe()
    {
        string name = AttributeContext.Name(Attribute);
        string symbol = Compare.ToSymbol();
        return $"{name} {symbol} {Value}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        if (!TryGetValue(context, out float current))
        {
            return false;
        }

        return Compare.Test(current, Value);
    }

    protected override bool TryScore(ConditionContext context, out ConditionScore score)
    {
        if (!TryGetValue(context, out float current))
        {
            score = default;
            return false;
        }

        return Compare.TryScore(current, Value, out score);
    }

    private bool TryGetValue(ConditionContext context, out float current)
    {
        current = 0;
        if (!AttributeContext.TryGetOwner(context, Subject, out AttributeSystem attributes, out Entity owner) || !attributes.HasAttribute(owner, Attribute))
        {
            return false;
        }

        current = attributes.GetValue(owner, Attribute);
        return true;
    }
}
