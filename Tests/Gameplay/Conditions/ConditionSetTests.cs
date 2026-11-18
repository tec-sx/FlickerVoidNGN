using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

public class ConditionSetTests
{
    private readonly FactDatabase _facts = new(new FactSettings());
    private readonly ConditionContext _context;

    public ConditionSetTests()
    {
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void PassesWhenEmptyInEitherMode()
    {
        ConditionSet all = new();
        ConditionSet any = new() { Mode = ConditionMode.Any };

        bool allHolds = all.Evaluate(_context);
        bool anyHolds = any.Evaluate(_context);

        Assert.True(allHolds);
        Assert.True(anyHolds);
    }

    [Fact]
    public void NeedsEveryConditionInAllMode()
    {
        ConditionSet set = new() { Conditions = [Constant(true), Constant(true)] };
        bool allTrue = set.Evaluate(_context);

        set.Conditions.Add(Constant(false));
        bool oneFalse = set.Evaluate(_context);

        Assert.True(allTrue);
        Assert.False(oneFalse);
    }

    [Fact]
    public void NeedsOneConditionInAnyMode()
    {
        ConditionSet set = new() { Mode = ConditionMode.Any, Conditions = [Constant(false), Constant(false)] };
        bool allFalse = set.Evaluate(_context);

        set.Conditions.Add(Constant(true));
        bool oneTrue = set.Evaluate(_context);

        Assert.False(allFalse);
        Assert.True(oneTrue);
    }

    [Fact]
    public void FlipsNestedGroupWithInvert()
    {
        GroupCondition inner = new() { Invert = true, Set = new ConditionSet { Conditions = [Constant(false)] } };
        ConditionSet set = new() { Conditions = [inner] };

        bool holds = set.Evaluate(_context);

        Assert.True(holds);
    }

    [Fact]
    public void RanksSetsThatHoldHardestFirst()
    {
        _facts.SetFact(TestTags.A, 50);

        ConditionSet[] sets =
        [
            AtLeast(20),
            AtLeast(80),
            AtLeast(40),
            new ConditionSet { Conditions = [Constant(true)] },
        ];

        int[] ranked = ConditionSet.Rank(sets, _context);

        Assert.Equal([2, 0, 3], ranked);
    }

    [Fact]
    public void ScoresAllSetsAsSumAndAnySetsAsBest()
    {
        _facts.SetFact(TestTags.A, 50);
        _facts.SetFact(TestTags.B, 30);

        FactCondition a = new() { Fact = TestTags.A, Value = 40 };
        FactCondition b = new() { Fact = TestTags.B, Value = 20 };
        ConditionSet all = new() { Conditions = [a, b] };
        ConditionSet any = new() { Mode = ConditionMode.Any, Conditions = [a, b] };

        bool allScored = all.TryGetScore(_context, out ConditionScore allScore);
        bool anyScored = any.TryGetScore(_context, out ConditionScore anyScore);

        Assert.True(allScored);
        Assert.Equal(new ConditionScore(60f, 20f), allScore);
        Assert.True(anyScored);
        Assert.Equal(40f, anyScore.Threshold);
    }

    [Fact]
    public void DoesNotScoreInvertedConditions()
    {
        _facts.SetFact(TestTags.A, 50);
        FactCondition inverted = new() { Fact = TestTags.A, Value = 80, Invert = true };

        bool holds = inverted.Check(_context);
        bool scored = inverted.TryGetScore(_context, out _);

        Assert.True(holds);
        Assert.False(scored);
    }

    [Fact]
    public void DescribesWithAndOrAndNot()
    {
        FactCondition a = new() { Fact = TestTags.A, Value = 4 };
        FactCondition notB = new() { Fact = TestTags.B, Compare = FactCompare.Defined, Invert = true };
        ConditionSet all = new() { Conditions = [a, notB] };
        ConditionSet any = new() { Mode = ConditionMode.Any, Conditions = [a, notB] };

        string allText = all.Describe();
        string anyText = any.Describe();

        Assert.Equal("Test.FVFramework.A >= 4 AND NOT Test.FVFramework.B defined", allText);
        Assert.Equal("Test.FVFramework.A >= 4 OR NOT Test.FVFramework.B defined", anyText);
    }

    /// <summary>An empty group passes, so an inverted one fails: constants without a test-only condition type.</summary>
    private static GroupCondition Constant(bool value) => new() { Invert = !value };

    private static ConditionSet AtLeast(int value) => new() { Conditions = [new FactCondition { Fact = TestTags.A, Value = value }] };
}
