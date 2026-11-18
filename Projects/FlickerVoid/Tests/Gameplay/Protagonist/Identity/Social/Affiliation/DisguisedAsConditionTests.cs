using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Affiliation;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Affiliation;

public class DisguisedAsConditionTests
{
    private readonly FactionDefinition _police;
    private readonly DisguisedAsConditionTestsBuilder _builder;

    public DisguisedAsConditionTests()
    {
        _police = new FactionDefinition
        {
            Display = new() { Name = "Police" },
            Standing = new ScaleDefinition { Id = new Tag("Fact.Social.Standing.Police") },
        };
        _builder = new DisguisedAsConditionTestsBuilder(_police);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoldsWhenTheInstigatorPassesAsAMember(bool disguised)
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.IsDisguisedAs(context.Instigator, _police).Returns(disguised);
        DisguisedAsCondition condition = _builder.Build();

        bool holds = condition.Check(context);

        holds.Should().Be(disguised);
    }

    [Fact]
    public void ChecksTheTargetWhenAsked()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.IsDisguisedAs(context.Target, _police).Returns(true);
        DisguisedAsCondition condition = _builder.WithSubject(ContextEntity.Target).Build();

        bool holds = condition.Check(context);

        holds.Should().BeTrue();
    }

    [Fact]
    public void FailsWithoutSocialRules()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        ConditionContext context = new(facts);
        DisguisedAsCondition condition = _builder.Build();

        bool holds = condition.Check(context);

        holds.Should().BeFalse();
    }

    [Fact]
    public void DescribesTheFaction()
    {
        DisguisedAsCondition condition = _builder.Build();

        string text = condition.Describe();

        text.Should().Be("Disguised as Police");
    }
}
