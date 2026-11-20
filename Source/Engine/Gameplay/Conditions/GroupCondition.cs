namespace FlickerVoid.Engine.Gameplay.Conditions;

/// <summary>A nested condition set, so All and Any can be combined.</summary>
public sealed class GroupCondition : Condition
{
    public ConditionSet Set { get; init; } = new();

    public override string Describe() => Set.Describe();

    protected override bool Evaluate(ConditionContext context) => Set.Evaluate(context);

    protected override bool TryScore(ConditionContext context, out ConditionScore score) => Set.TryGetScore(context, out score);
}
