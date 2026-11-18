using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Definitions;
using FlickerVoid.Engine.Gameplay.Scales;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Gameplay.Protagonist.Identity.Social.Relationships;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Identity.Social.Relationships;

public class ModifyRelationshipEffectTests
{
    private readonly ModifyRelationshipEffectTestsBuilder _builder = new();
    private readonly CharacterDefinition _mara = new() { Display = new() { Name = "Mara" } };
    private readonly ScaleDefinition _relationship = new() { Id = new Tag("Fact.Relationship.Mara") };

    public ModifyRelationshipEffectTests()
    {
        _builder.Rules.GetRelationshipScale(_mara).Returns(_relationship);
    }

    [Fact]
    public void ChangesTheRelationshipWithTheCharacter()
    {
        ModifyRelationshipEffect effect = _builder.WithCharacter(_mara).WithDelta(5).Build();

        effect.Apply(_builder.Context);

        int value = _builder.Facts.GetFact(_relationship.Id);
        value.Should().Be(5);
    }

    [Fact]
    public void WithoutACharacterChangesTheTargets()
    {
        ConditionContext context = _builder.Context;
        _builder.Rules.GetCharacter(context.Target).Returns(_mara);
        ModifyRelationshipEffect effect = _builder.WithDelta(-3).Build();

        effect.Apply(context);

        int value = _builder.Facts.GetFact(_relationship.Id);
        value.Should().Be(-3);
    }

    [Fact]
    public void DescribesTheCharacterOrTheTarget()
    {
        ModifyRelationshipEffect named = _builder.WithCharacter(_mara).WithDelta(5).Build();
        ModifyRelationshipEffect target = new ModifyRelationshipEffectTestsBuilder().WithDelta(-3).Build();

        string namedText = named.Describe();
        string targetText = target.Describe();

        namedText.Should().Be("Relationship with Mara 5");
        targetText.Should().Be("Relationship with Target -3");
    }
}
