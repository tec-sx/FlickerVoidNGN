using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

public class FactEffectTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly ConditionContext _context;
    private readonly FactEffectTestsBuilder _builder = new();

    public FactEffectTests()
    {
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void SetsFact()
    {
        FactEffect effect = _builder.Writing(FactWrite.Set, 3).Build();

        effect.Apply(_context);

        _facts.Received(1).SetFact(TestTags.A, 3);
    }

    [Fact]
    public void AddsToFact()
    {
        FactEffect effect = _builder.Writing(FactWrite.Add, 2).Build();

        effect.Apply(_context);

        _facts.Received(1).AddFact(TestTags.A, 2);
    }

    [Fact]
    public void RemovesFact()
    {
        FactEffect effect = _builder.Writing(FactWrite.Remove, 0).Build();

        effect.Apply(_context);

        _facts.Received(1).RemoveFact(TestTags.A);
    }

    [Theory]
    [InlineData(FactWrite.Set, "Test.FVFramework.A = 2")]
    [InlineData(FactWrite.Add, "Test.FVFramework.A += 2")]
    [InlineData(FactWrite.Remove, "Remove Test.FVFramework.A")]
    public void DescribesWrite(FactWrite write, string expected)
    {
        FactEffect effect = _builder.Writing(write, 2).Build();

        string text = effect.Describe();

        text.Should().Be(expected);
    }
}
