using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;

namespace FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

/// <summary>How an observer's faction treats the other context entity, at least. In dialogue the observer is the Target (the character spoken to); in a character's own logic it is the Instigator.</summary>
public sealed class AttitudeCondition : Condition
{
    public ContextEntity Observer { get; init; } = ContextEntity.Target;

    /// <summary>Overrides the observer's own faction.</summary>
    public FactionDefinition? Faction { get; init; }

    public Attitude AtLeast { get; init; } = Attitude.Neutral;

    public override string Describe()
    {
        string factionName = "Observer's faction";
        if (Faction is not null)
        {
            factionName = Faction.Display.Name;
        }

        return $"{factionName} at least {AtLeast}";
    }

    protected override bool Evaluate(ConditionContext context)
    {
        ISocialRules? rules = context.GetService<ISocialRules>();
        if (rules is null)
        {
            return false;
        }

        ContextEntity subjectEntity = ContextEntity.Instigator;
        if (Observer == ContextEntity.Instigator)
        {
            subjectEntity = ContextEntity.Target;
        }

        IEntityView? observer = context.GetEntity(Observer);
        IEntityView? subject = context.GetEntity(subjectEntity);
        FactionDefinition? faction = Faction ?? rules.GetFaction(observer);
        if (faction is null)
        {
            return false;
        }

        Attitude attitude = rules.GetAttitude(faction, subject);
        return attitude >= AtLeast;
    }
}
