using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Facts;

/// <summary>Builds the fact condition under test, on fact A.</summary>
internal sealed class FactConditionTestsBuilder
{
    private FactCompare _compare = FactCompare.GreaterOrEqual;
    private int _value = 1;
    private bool _invert;

    public FactConditionTestsBuilder Comparing(FactCompare compare, int value)
    {
        _compare = compare;
        _value = value;
        return this;
    }

    public FactConditionTestsBuilder Inverted()
    {
        _invert = true;
        return this;
    }

    public FactCondition Build() => new() { Fact = TestTags.A, Compare = _compare, Value = _value, Invert = _invert };
}
