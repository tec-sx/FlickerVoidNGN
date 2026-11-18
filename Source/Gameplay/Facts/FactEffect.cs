using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Sets, adds to or removes a fact.</summary>
public sealed class FactEffect : Effect
{
    public Tag Fact { get; init; }

    public FactWrite Write { get; init; } = FactWrite.Set;

    /// <summary>Unused by Remove.</summary>
    public int Value { get; init; } = 1;

    public override void Apply(ConditionContext context)
    {
        switch (Write)
        {
            case FactWrite.Set:
                context.Facts.SetFact(Fact, Value);
                break;
            case FactWrite.Add:
                context.Facts.AddFact(Fact, Value);
                break;
            case FactWrite.Remove:
                context.Facts.RemoveFact(Fact);
                break;
        }
    }

    public override string Describe()
    {
        switch (Write)
        {
            case FactWrite.Set:
                return $"{Fact} = {Value}";
            case FactWrite.Add:
                return $"{Fact} += {Value}";
            default:
                return $"Remove {Fact}";
        }
    }
}
