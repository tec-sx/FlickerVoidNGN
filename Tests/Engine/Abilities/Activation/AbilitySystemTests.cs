using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Abilities.Rules;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

using B = FlickerVoid.Engine.Abilities.Tests.Activation.AbilitySystemTestsBuilder;

namespace FlickerVoid.Engine.Abilities.Tests.Activation;

public sealed class AbilitySystemTests : IDisposable
{
    private readonly AbilitySystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheSetsGrantTheirAbilities()
    {
        _builder.Build();

        IEnumerable<Tag> granted = _builder.Abilities.GetAbilities(_builder.Hero).Select(spec => spec.Ability.Id);

        granted.Should().Equal(B.Sneak, B.Talk, B.Hide, B.Examine);
        _builder.Abilities.GetAbilities(_builder.Guard).Should().BeEmpty();
    }

    [Fact]
    public void AHeldAbilityRunsWhileItsInputIsHeldAndOwnsItsTags()
    {
        _builder.Build();
        List<AbilityActivatedEvent> activated = _builder.Collect<AbilityActivatedEvent>();
        List<AbilityEndedEvent> ended = _builder.Collect<AbilityEndedEvent>();

        _builder.Abilities.InputPressed(_builder.Hero, B.SneakInput);
        bool active = _builder.Abilities.IsActive(_builder.Hero, B.Sneak);
        bool sneaking = _builder.HeroHasTag("Status.Sneaking");
        _builder.Abilities.InputReleased(_builder.Hero, B.SneakInput);
        _builder.Dispatch();

        active.Should().BeTrue();
        sneaking.Should().BeTrue();
        _builder.Abilities.IsActive(_builder.Hero, B.Sneak).Should().BeFalse();
        _builder.HeroHasTag("Status.Sneaking").Should().BeFalse();
        activated.Should().ContainSingle().Which.Ability.Id.Should().Be(B.Sneak);
        ended.Should().ContainSingle().Which.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void CostsArePaidAndAnAbilityThatCantPayFailsWithItsMessage()
    {
        _builder.Build(stamina: 3);
        List<AbilityFailedEvent> failed = _builder.Collect<AbilityFailedEvent>();

        bool first = _builder.Abilities.TryActivate(_builder.Hero, B.Sneak);
        _builder.Abilities.End(_builder.Hero, B.Sneak);
        bool second = _builder.Abilities.TryActivate(_builder.Hero, B.Sneak);
        _builder.Dispatch();

        first.Should().BeTrue();
        second.Should().BeFalse();
        _builder.Attributes.GetValue(_builder.Hero, B.Stamina).Should().Be(1);
        AbilityFailedEvent failure = failed.Should().ContainSingle().Subject;
        failure.Reason.Should().Be(AbilityFailure.Cost);
        failure.Message.Should().Be("Too tired to sneak.");
    }

    [Fact]
    public void AnActiveAbilityCantStartAgainAndAnUnknownOneIsMissing()
    {
        _builder.Build();

        _builder.Abilities.TryActivate(_builder.Hero, B.Talk).Should().BeTrue();
        _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out Tag active).Should().BeFalse();
        _builder.Abilities.CanActivate(_builder.Hero, new Tag("Ability.Fly"), out Tag missing).Should().BeFalse();

        active.Should().Be(AbilityFailure.Active);
        missing.Should().Be(AbilityFailure.Missing);
    }

    [Fact]
    public void BlockedTagsOnTheOwnerStopAnAbility()
    {
        _builder.Build();

        _builder.Abilities.TryActivate(_builder.Hero, B.Sneak);
        bool talks = _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out Tag reason);

        talks.Should().BeFalse();
        reason.Should().Be(AbilityFailure.TagsBlocked);
    }

    [Fact]
    public void AnAbilityCancelsAndBlocksOthersByTheirTags()
    {
        _builder.Build();
        List<AbilityEndedEvent> ended = _builder.Collect<AbilityEndedEvent>();

        _builder.Abilities.TryActivate(_builder.Hero, B.Sneak);
        _builder.Abilities.TryActivate(_builder.Hero, B.Hide);
        bool talks = _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out Tag reason);
        _builder.Abilities.End(_builder.Hero, B.Hide);
        bool talksAfter = _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out _);
        _builder.Dispatch();

        _builder.Abilities.IsActive(_builder.Hero, B.Sneak).Should().BeFalse();
        talks.Should().BeFalse();
        reason.Should().Be(AbilityFailure.Blocked);
        talksAfter.Should().BeTrue();
        ended.Select(end => (end.Ability.Id, end.Cancelled)).Should().Equal((B.Sneak, true), (B.Hide, false));
    }

    [Fact]
    public void ExclusiveAbilitiesReplaceOrBlockEachOther()
    {
        _builder.WithAbility("Read", """{ "type": "ability", "id": "Ability.Read", "group": "ExclusiveReplace" }""")
            .WithAbility("Pick", """{ "type": "ability", "id": "Ability.Pick", "group": "ExclusiveBlock" }""")
            .Build();
        Tag read = new("Ability.Read");
        Tag pick = new("Ability.Pick");

        _builder.Abilities.TryActivate(_builder.Hero, B.Talk);
        _builder.Abilities.TryActivate(_builder.Hero, read);
        bool talkReplaced = !_builder.Abilities.IsActive(_builder.Hero, B.Talk);
        _builder.Abilities.TryActivate(_builder.Hero, pick);
        bool readReplaced = !_builder.Abilities.IsActive(_builder.Hero, read);
        bool talks = _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out Tag reason);

        talkReplaced.Should().BeTrue();
        readReplaced.Should().BeTrue();
        talks.Should().BeFalse();
        reason.Should().Be(AbilityFailure.Group);
        _builder.Abilities.TryActivate(_builder.Hero, B.Hide).Should().BeTrue();
    }

    [Fact]
    public void AnInstantAbilityAppliesItsEffectsOnItsTargetAndEnds()
    {
        _builder.Build();
        List<AbilityEndedEvent> ended = _builder.Collect<AbilityEndedEvent>();

        bool examined = _builder.Abilities.TryActivate(_builder.Hero, B.Examine, _builder.Guard);
        _builder.Dispatch();

        examined.Should().BeTrue();
        _builder.Game.Facts.GetFact(new Tag("Fact.Examined")).Should().Be(1);
        _builder.Abilities.IsActive(_builder.Hero, B.Examine).Should().BeFalse();
        AbilityEndedEvent end = ended.Should().ContainSingle().Subject;
        end.Cancelled.Should().BeFalse();
        end.Target.Should().Be(_builder.Guard);
    }

    [Fact]
    public void TagRelationshipsAddRequirementsToAbilitiesWithTheirTag()
    {
        _builder.WithFile("Abilities/Relationships.fvdata", """
            { "type": "abilityTagRelationships", "relationships": [
                { "abilityTag": "Ability.Social", "requiredTags": [ "Status.Awake" ] } ] }
            """).Build(abilities: """ "relationships": "Abilities/Relationships.fvdata" """);

        bool talks = _builder.Abilities.CanActivate(_builder.Hero, B.Talk, out Tag reason);

        talks.Should().BeFalse();
        reason.Should().Be(AbilityFailure.TagsMissing);
    }

    [Fact]
    public void AbilitiesGrantedToActivateDoSoWhenTheEntityStarts()
    {
        _builder.WithAbility("Breathe", """{ "type": "ability", "id": "Ability.Breathe", "policy": "OnGranted" }""").Build();

        _builder.Abilities.IsActive(_builder.Hero, new Tag("Ability.Breathe")).Should().BeTrue();
    }

    [Fact]
    public void TheRulesActivateAndCheckAbilitiesOfTheSubject()
    {
        _builder.Build();
        ConditionContext context = _builder.Context();
        CanActivateAbilityCondition canTalk = new() { Ability = B.Talk };
        CanActivateAbilityCondition guardTalks = new() { Ability = B.Talk, Subject = ContextEntity.Target };

        bool before = canTalk.Check(context);
        new ActivateAbilityEffect { Ability = B.Talk }.Apply(context);

        before.Should().BeTrue();
        guardTalks.Check(context).Should().BeFalse();
        _builder.Abilities.IsActive(_builder.Hero, B.Talk).Should().BeTrue();
        _builder.Abilities.GetAbilities(_builder.Hero)[1].Target.Should().Be(_builder.Guard);
        canTalk.Check(context).Should().BeFalse();
    }
}
