using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

/// <summary>The subject passes as a member of the faction: wears its disguise and isn't too notorious for it to work.</summary>
public sealed class DisguisedAsCondition : Condition
{
    public required FactionDefinition Faction { get; init; }

    public ContextEntity Subject { get; init; } = ContextEntity.Instigator;

    public override string Describe() => $"Disguised as {Faction.Display.Name}";

    protected override bool Evaluate(ConditionContext context)
    {
        ISocialRules? rules = context.GetService<ISocialRules>();
        IEntityView? subject = context.GetEntity(Subject);
        return rules is not null && rules.IsDisguisedAs(subject, Faction);
    }
}
