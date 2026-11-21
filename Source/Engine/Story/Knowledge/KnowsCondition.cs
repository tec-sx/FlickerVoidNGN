using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Story.Knowledge;

/// <summary>Whether a piece of knowledge is known, read from its fact.</summary>
public sealed class KnowsCondition : Condition
{
    public Tag Knowledge { get; set; }

    public override string Describe() => $"Knows {Knowledge}";

    protected override bool Evaluate(ConditionContext context) => context.Facts.GetFact(Knowledge) > 0;
}
