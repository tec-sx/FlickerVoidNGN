using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

/// <summary>Builds the condition set under test.</summary>
internal sealed class ConditionSetTestsBuilder
{
    private readonly List<Condition> _conditions = [];
    private ConditionMode _mode = ConditionMode.All;

    public ConditionSetTestsBuilder InAnyMode()
    {
        _mode = ConditionMode.Any;
        return this;
    }

    public ConditionSetTestsBuilder With(Condition condition)
    {
        _conditions.Add(condition);
        return this;
    }

    /// <summary>An empty group holds and an inverted one fails: constants without a test-only condition type.</summary>
    public ConditionSetTestsBuilder WithConstant(bool holds)
    {
        GroupCondition constant = new() { Invert = !holds };
        return With(constant);
    }

    public ConditionSetTestsBuilder WithAtLeast(Tag fact, int value)
    {
        FactCondition condition = new() { Fact = fact, Value = value };
        return With(condition);
    }

    public ConditionSet Build() => new() { Mode = _mode, Conditions = [.. _conditions] };
}
