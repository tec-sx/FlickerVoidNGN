using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

/// <summary>Changes the player's relationship with a character; without a Character it is the Target's.</summary>
public sealed class ModifyRelationshipEffect : Effect
{
    public CharacterDefinition? Character { get; init; }

    public int Delta { get; init; }

    public override void Apply(ConditionContext context)
    {
        ScaleDefinition? relationship = FindRelationship(context);
        relationship?.Modify(context.Facts, Delta);
    }

    public override string Describe()
    {
        string characterName = Character?.Display.Name ?? "Target";
        return $"Relationship with {characterName} {Delta}";
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
