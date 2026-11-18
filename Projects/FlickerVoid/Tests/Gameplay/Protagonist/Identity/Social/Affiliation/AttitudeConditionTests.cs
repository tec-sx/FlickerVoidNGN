using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

public class AttitudeConditionTests
{
    private readonly AttitudeConditionTestsBuilder _builder = new();
    private readonly FactionDefinition _police = new()
    {
        Display = new() { Name = "Police" },
        Standing = new ScaleDefinition { Id = new Tag("Fact.Social.Standing.Police") },
    };

    [Theory]
    [InlineData(Attitude.Unfriendly, false)]
    [InlineData(Attitude.Friendly, true)]
    [InlineData(Attitude.Allied, true)]
    public void HoldsWhenTheTargetsFactionTreatsTheInstigatorAtLeastAsWell(Attitude attitude, bool expected)
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetFaction(context.Target).Returns(_police);
        _builder.Rules.GetAttitude(_police, context.Instigator).Returns(attitude);
        AttitudeCondition condition = _builder.WithAtLeast(Attitude.Friendly).Build();

        bool holds = condition.Check(context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void AnInstigatorObserverJudgesTheTarget()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetFaction(context.Instigator).Returns(_police);
        _builder.Rules.GetAttitude(_police, context.Target).Returns(Attitude.Hostile);
        AttitudeCondition condition = _builder.WithObserver(ContextEntity.Instigator).WithAtLeast(Attitude.Friendly).Build();

        bool holds = condition.Check(context);

        holds.Should().BeFalse();
    }

    [Fact]
    public void TheFactionOverridesTheObserversOwn()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetAttitude(_police, context.Instigator).Returns(Attitude.Allied);
        AttitudeCondition condition = _builder.WithFaction(_police).WithAtLeast(Attitude.Allied).Build();

        bool holds = condition.Check(context);

        holds.Should().BeTrue();
        _builder.Rules.DidNotReceive().GetFaction(context.Target);
    }

    [Fact]
    public void FailsWithoutAnObserverFaction()
    {
        AttitudeCondition condition = _builder.WithAtLeast(Attitude.Hostile).Build();

        bool holds = condition.Check(_builder.Context);

        holds.Should().BeFalse();
    }

    [Fact]
    public void FailsWithoutSocialRules()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        ConditionContext context = new(facts);
        AttitudeCondition condition = _builder.WithFaction(_police).WithAtLeast(Attitude.Hostile).Build();

        bool holds = condition.Check(context);

        holds.Should().BeFalse();
    }

    [Fact]
    public void DescribesTheFactionAndAttitude()
    {
        AttitudeCondition withFaction = _builder.WithFaction(_police).WithAtLeast(Attitude.Friendly).Build();
        AttitudeCondition withoutFaction = new AttitudeConditionTestsBuilder().Build();

        string named = withFaction.Describe();
        string observers = withoutFaction.Describe();

        named.Should().Be("Police at least Friendly");
        observers.Should().Be("Observer's faction at least Neutral");
    }
}
