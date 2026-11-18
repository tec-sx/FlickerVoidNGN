using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

/// <summary>Builds the relationship condition under test and a context with substitute facts and social rules.</summary>
internal sealed class RelationshipConditionTestsBuilder
{
    private CharacterDefinition? _character;
    private Compare _compare = Compare.GreaterOrEqual;
    private int _value;

    public RelationshipConditionTestsBuilder()
    {
        Context = SocialContext.Create(Facts, (typeof(ISocialRules), Rules));
    }

    public IFactDatabase Facts { get; } = Substitute.For<IFactDatabase>();

    public ISocialRules Rules { get; } = Substitute.For<ISocialRules>();

    public ConditionContext Context { get; }

    public RelationshipConditionTestsBuilder WithCharacter(CharacterDefinition character)
    {
        _character = character;
        return this;
    }

    public RelationshipConditionTestsBuilder WithCompare(Compare compare, int value)
    {
        _compare = compare;
        _value = value;
        return this;
    }

    public RelationshipCondition Build() => new() { Character = _character, Compare = _compare, Value = _value };
}
