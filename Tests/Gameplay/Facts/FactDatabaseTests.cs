using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

public class FactDatabaseTests
{
    private readonly FactSettings _settings = new();
    private readonly FactDatabase _facts;
    private readonly ConditionContext _context;

    public FactDatabaseTests()
    {
        _facts = new FactDatabase(_settings);
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void ReadsZeroForUndefinedFact()
    {
        Assert.False(_facts.IsFactDefined(TestTags.A));
        Assert.Equal(0, _facts.GetFact(TestTags.A));
    }

    [Fact]
    public void SetsAddsAndRemoves()
    {
        _facts.SetFact(TestTags.A, 5);
        Assert.True(_facts.IsFactDefined(TestTags.A));
        Assert.Equal(5, _facts.GetFact(TestTags.A));

        _facts.AddFact(TestTags.A, 3);
        Assert.Equal(8, _facts.GetFact(TestTags.A));

        _facts.AddFact(TestTags.B);
        Assert.Equal(1, _facts.GetFact(TestTags.B));

        bool removed = _facts.RemoveFact(TestTags.A);
        Assert.True(removed);
        Assert.False(_facts.IsFactDefined(TestTags.A));

        bool removedTwice = _facts.RemoveFact(TestTags.A);
        Assert.False(removedTwice);
    }

    [Fact]
    public void RemovesOnlyFactsUnderParentTag()
    {
        _facts.SetFact(TestTags.GroupOne, 1);
        _facts.SetFact(TestTags.GroupTwo, 2);
        _facts.SetFact(TestTags.A, 3);

        int removed = _facts.RemoveFactsUnder(TestTags.Group);

        Assert.Equal(2, removed);
        Assert.False(_facts.IsFactDefined(TestTags.GroupOne));
        Assert.False(_facts.IsFactDefined(TestTags.GroupTwo));
        Assert.True(_facts.IsFactDefined(TestTags.A));
    }

    [Fact]
    public void NotifiesListenersWithOldAndNewValue()
    {
        _facts.SetFact(TestTags.A, 2);

        List<(int Old, int New)> changes = [];
        FactChangedHandler handler = (tag, oldValue, newValue) =>
        {
            if (tag == TestTags.A)
            {
                changes.Add((oldValue, newValue));
            }
        };

        _facts.FactChanged += handler;
        _facts.AddFact(TestTags.A, 3);
        _facts.RemoveFact(TestTags.A);
        _facts.FactChanged -= handler;

        Assert.Equal([(2, 5), (5, 0)], changes);
        Assert.Equal(TestTags.A, _facts.LastChangedTag);
    }

    [Fact]
    public void ClampsFactWithRangeInSettings()
    {
        _settings.Definitions.Add(new FactDefinition { Tag = TestTags.A, Clamp = true, Min = -10, Max = 10 });

        _facts.SetFact(TestTags.A, 50);
        Assert.Equal(10, _facts.GetFact(TestTags.A));

        _facts.AddFact(TestTags.A, -100);
        Assert.Equal(-10, _facts.GetFact(TestTags.A));
    }

    [Fact]
    public void ResetsToClampedDefaultsWithValueNames()
    {
        _settings.Definitions.Add(new FactDefinition { Tag = TestTags.A, DefaultValue = 1, ValueNames = ["Closed", "Open"] });
        _settings.Definitions.Add(new FactDefinition { Tag = TestTags.B, DefaultValue = 50, Clamp = true, Min = 0, Max = 10 });
        _facts.SetFact(TestTags.GroupOne, 1);

        _facts.ResetToDefaults();

        string? valueName = _facts.GetFactValueName(TestTags.A);

        Assert.Equal(1, _facts.GetFact(TestTags.A));
        Assert.Equal("Open", valueName);
        Assert.Equal(10, _facts.GetFact(TestTags.B));
        Assert.False(_facts.IsFactDefined(TestTags.GroupOne));
    }

    [Fact]
    public void RefusesToWriteNoTag()
    {
        Assert.Throws<ArgumentException>(() => _facts.SetFact(Tag.None, 1));
    }

    [Fact]
    public void EvaluatesFactConditions()
    {
        _facts.SetFact(TestTags.A, 4);

        ConditionSet set = new()
        {
            Conditions =
            [
                new FactCondition { Fact = TestTags.A, Compare = FactCompare.GreaterOrEqual, Value = 4 },
                new FactCondition { Fact = TestTags.A, Compare = FactCompare.Defined },
                new FactCondition { Fact = TestTags.B, Compare = FactCompare.Undefined },
            ],
        };
        bool holds = set.Evaluate(_context);

        set.Conditions.Add(new FactCondition { Fact = TestTags.A, Compare = FactCompare.Greater, Value = 4 });
        bool holdsAbove = set.Evaluate(_context);

        Assert.True(holds);
        Assert.False(holdsAbove);
    }

    [Fact]
    public void AppliesFactEffectsInOrder()
    {
        EffectList effects = new()
        {
            Effects =
            [
                new FactEffect { Fact = TestTags.A, Write = FactWrite.Set, Value = 3 },
                new FactEffect { Fact = TestTags.A, Write = FactWrite.Add, Value = 2 },
                new FactEffect { Fact = TestTags.B, Write = FactWrite.Set, Value = 1 },
                new FactEffect { Fact = TestTags.B, Write = FactWrite.Remove },
            ],
        };

        effects.Apply(_context);

        Assert.Equal(5, _facts.GetFact(TestTags.A));
        Assert.False(_facts.IsFactDefined(TestTags.B));
    }

    [Fact]
    public void DescribesFactConditionsAndEffects()
    {
        FactCondition atLeast = new() { Fact = TestTags.A, Value = 4 };
        FactCondition defined = new() { Fact = TestTags.A, Compare = FactCompare.Defined };
        FactEffect add = new() { Fact = TestTags.A, Write = FactWrite.Add, Value = 2 };

        string atLeastText = atLeast.Describe();
        string definedText = defined.Describe();
        string addText = add.Describe();

        Assert.Equal("Test.FVFramework.A >= 4", atLeastText);
        Assert.Equal("Test.FVFramework.A defined", definedText);
        Assert.Equal("Test.FVFramework.A += 2", addText);
    }
}
