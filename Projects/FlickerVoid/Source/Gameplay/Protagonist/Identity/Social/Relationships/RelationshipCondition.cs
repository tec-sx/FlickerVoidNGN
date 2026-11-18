using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

/// <summary>The player's relationship with a character; without a Character it is the Target's, so one dialogue line works for every character. At-least comparisons are scored.</summary>
public sealed class RelationshipCondition : Condition
{
    public CharacterDefinition? Character { get; init; }

    public Compare Compare { get; init; } = Compare.GreaterOrEqual;

    public int Value { get; init; }

    public override string Describe()
    {
        string characterName = Character?.Display.Name ?? "Target";
        string symbol = Compare.ToSymbol();
        return $"Relationship with {characterName} {symbol} {Value}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        ScaleDefinition? relationship = FindRelationship(context);
        if (relationship is null)
        {
            return false;
        }

        int current = relationship.GetValue(context.Facts);
        return Compare.Test(current, Value);
    }

    protected override bool TryScore(ConditionContext context, out ConditionScore score)
    {
        ScaleDefinition? relationship = FindRelationship(context);
        if (relationship is null)
        {
            score = default;
            return false;
        }

        int current = relationship.GetValue(context.Facts);
        return Compare.TryScore(current, Value, out score);
    }

    /// <summary>The character's relationship scale, or the Target's when no character is set; null without social rules.</summary>
    private ScaleDefinition? FindRelationship(ConditionContext context)
    {
        ISocialRules? rules = context.GetService<ISocialRules>();
        if (rules is null)
        {
            return null;
        }

        CharacterDefinition? character = Character ?? rules.GetCharacter(context.Target);
        return rules.GetRelationshipScale(character);
    }
}
