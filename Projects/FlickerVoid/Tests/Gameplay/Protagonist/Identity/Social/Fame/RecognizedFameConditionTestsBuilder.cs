using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Fame;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Fame;

/// <summary>Builds the recognized-fame condition under test and a context whose services hold substitute social rules.</summary>
internal sealed class RecognizedFameConditionTestsBuilder
{
    private Compare _compare = Compare.GreaterOrEqual;
    private int _value;

    public RecognizedFameConditionTestsBuilder()
    {
        IFactDatabase facts = Substitute.For<IFactDatabase>();
        Context = SocialContext.Create(facts, (typeof(ISocialRules), Rules));
    }

    public ISocialRules Rules { get; } = Substitute.For<ISocialRules>();

    public ConditionContext Context { get; }

    public RecognizedFameConditionTestsBuilder WithCompare(Compare compare, int value)
    {
        _compare = compare;
        _value = value;
        return this;
    }

    public RecognizedFameCondition Build() => new() { Compare = _compare, Value = _value };
}
