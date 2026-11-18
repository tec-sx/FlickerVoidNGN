using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

/// <summary>Builds the attitude condition under test and a context whose services hold substitute social rules.</summary>
internal sealed class AttitudeConditionTestsBuilder
{
    private ContextEntity _observer = ContextEntity.Target;
    private FactionDefinition? _faction;
    private Attitude _atLeast = Attitude.Neutral;

    public AttitudeConditionTestsBuilder()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        Context = SocialContext.Create(facts, (typeof(ISocialRules), Rules));
    }

    public ISocialRules Rules { get; } = Substitute.For<ISocialRules>();

    public ConditionContext Context { get; }

    public AttitudeConditionTestsBuilder WithObserver(ContextEntity observer)
    {
        _observer = observer;
        return this;
    }

    public AttitudeConditionTestsBuilder WithFaction(FactionDefinition faction)
    {
        _faction = faction;
        return this;
    }

    public AttitudeConditionTestsBuilder WithAtLeast(Attitude atLeast)
    {
        _atLeast = atLeast;
        return this;
    }

    public AttitudeCondition Build() => new() { Observer = _observer, Faction = _faction, AtLeast = _atLeast };
}
