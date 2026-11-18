using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

public class RelationshipConditionTests
{
    private readonly RelationshipConditionTestsBuilder _builder = new();
    private readonly CharacterDefinition _mara = new() { Display = new() { Name = "Mara" } };
    private readonly ScaleDefinition _relationship = new() { Id = new Tag("Fact.Relationship.Mara") };

    public RelationshipConditionTests()
    {
        _builder.Rules.GetRelationshipScale(_mara).Returns(_relationship);
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    public void ComparesTheRelationshipWithTheCharacter(int relationship, bool expected)
    {
        _builder.Facts.GetFact(_relationship.Id).Returns(relationship);
        RelationshipCondition condition = _builder.WithCharacter(_mara).WithCompare(Compare.GreaterOrEqual, 20).Build();

        bool holds = condition.Check(_builder.Context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void WithoutACharacterReadsTheTargets()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetCharacter(context.Target).Returns(_mara);
        _builder.Facts.GetFact(_relationship.Id).Returns(30);
        RelationshipCondition condition = _builder.WithCompare(Compare.GreaterOrEqual, 20).Build();

        bool holds = condition.Check(context);
        bool scored = condition.TryGetScore(context, out ConditionScore score);

        holds.Should().BeTrue();
        scored.Should().BeTrue();
        score.Should().Be(new ConditionScore(20, 10));
    }

    [Fact]
    public void FailsWithoutARelationshipScale()
    {
        RelationshipCondition condition = _builder.WithCompare(Compare.LessOrEqual, 0).Build();

        bool holds = condition.Check(_builder.Context);
        bool scored = condition.TryGetScore(_builder.Context, out _);

        holds.Should().BeFalse();
        scored.Should().BeFalse();
    }

    [Fact]
    public void DescribesTheCharacterOrTheTarget()
    {
        RelationshipCondition named = _builder.WithCharacter(_mara).WithCompare(Compare.GreaterOrEqual, 20).Build();
        RelationshipCondition target = new RelationshipConditionTestsBuilder().WithCompare(Compare.Less, 0).Build();

        string namedText = named.Describe();
        string targetText = target.Describe();

        namedText.Should().Be("Relationship with Mara >= 20");
        targetText.Should().Be("Relationship with Target < 0");
    }
}
