using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Gameplay.Protagonist.Identity.Social;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

/// <summary>Builds the modify-relationship effect under test and a context with real facts and substitute social rules.</summary>
internal sealed class ModifyRelationshipEffectTestsBuilder
{
    private CharacterDefinition? _character;
    private int _delta;

    public ModifyRelationshipEffectTestsBuilder()
    {
        Context = SocialContext.Create(Facts, (typeof(ISocialRules), Rules));
    }

    public FactDatabase Facts { get; } = new(new FactSettings());

    public ISocialRules Rules { get; } = Substitute.For<ISocialRules>();

    public ConditionContext Context { get; }

    public ModifyRelationshipEffectTestsBuilder WithCharacter(CharacterDefinition character)
    {
        _character = character;
        return this;
    }

    public ModifyRelationshipEffectTestsBuilder WithDelta(int delta)
    {
        _delta = delta;
        return this;
    }

    public ModifyRelationshipEffect Build() => new() { Character = _character, Delta = _delta };
}
