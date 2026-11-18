using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

/// <summary>Builds the disguised-as condition under test for a faction, and a context whose services hold substitute social rules.</summary>
internal sealed class DisguisedAsConditionTestsBuilder
{
    private readonly FactionDefinition _faction;
    private ContextEntity _subject = ContextEntity.Instigator;

    public DisguisedAsConditionTestsBuilder(FactionDefinition faction)
    {
        _faction = faction;
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        Context = SocialContext.Create(facts, (typeof(ISocialRules), Rules));
    }

    public ISocialRules Rules { get; } = Substitute.For<ISocialRules>();

    public ConditionContext Context { get; }

    public DisguisedAsConditionTestsBuilder WithSubject(ContextEntity subject)
    {
        _subject = subject;
        return this;
    }

    public DisguisedAsCondition Build() => new() { Faction = _faction, Subject = _subject };
}
