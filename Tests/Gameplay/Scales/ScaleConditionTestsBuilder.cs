using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

/// <summary>Builds the scale condition under test on the given scale.</summary>
internal sealed class ScaleConditionTestsBuilder(ScaleDefinition scale)
{
    private Compare _compare = Compare.GreaterOrEqual;
    private int _value;

    public ScaleConditionTestsBuilder Comparing(Compare compare, int value)
    {
        _compare = compare;
        _value = value;
        return this;
    }

    public ScaleCondition Build() => new() { Scale = scale, Compare = _compare, Value = _value };
}
