using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

public class HasTitleConditionTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly TitleDefinition _known = new() { Id = new Tag("Fact.Title.Known"), Display = new() { Name = "Known" } };
    private readonly HasTitleCondition _condition;
    private readonly ConditionContext _context;

    public HasTitleConditionTests()
    {
        _condition = new HasTitleConditionTestsBuilder(_known).Build();
        _context = new ConditionContext(_facts);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void HoldsOnceTitleIsEarned(int rank, bool expected)
    {
        _facts.GetFact(_known.Id).Returns(rank);

        bool holds = _condition.Check(_context);

        holds.Should().Be(expected);
    }

    [Fact]
    public void DescribesWithTitleName()
    {
        string text = _condition.Describe();

        text.Should().Be("Has title Known");
    }
}
