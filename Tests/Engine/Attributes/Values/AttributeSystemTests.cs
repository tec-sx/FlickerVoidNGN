using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Rules;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Attributes.Tests.Values;

public sealed class AttributeSystemTests : IDisposable
{
    private static readonly Tag Stealth = AttributeSystemTestsBuilder.Stealth;
    private static readonly Tag Health = AttributeSystemTestsBuilder.Health;

    private readonly AttributeSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ASaveBringsBackTheBaseValuesAndTheModifiersWithTheirHandles()
    {
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 6 }""");
        ModifierDefinition drunk = _builder.Load<ModifierDefinition>(AttributeSystemTestsBuilder.Drunk);
        AttributeSystem attributes = _builder.Attributes;
        attributes.SetBaseValue(_builder.Hero, AttributeSystemTestsBuilder.Stealth, 7);
        Guid handle = attributes.ApplyModifier(_builder.Hero, drunk);
        _builder.Saves.Save("Slot1");
        attributes.RemoveModifier(_builder.Hero, handle);
        attributes.SetBaseValue(_builder.Hero, AttributeSystemTestsBuilder.Stealth, 2);

        _builder.Saves.Load("Slot1");

        attributes.GetBaseValue(_builder.Hero, AttributeSystemTestsBuilder.Stealth).Should().Be(7);
        attributes.GetValue(_builder.Hero, AttributeSystemTestsBuilder.Stealth).Should().Be(5);
        attributes.GetModifiers(_builder.Hero).Should().ContainSingle().Which.Handle.Should().Be(handle);
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).HasMatchingTag(new Tag("Status.Drunk")).Should().BeTrue();
    }

    [Fact]
    public void StartingValuesAreClampedAndRoundedByTheirDefinitions()
    {
        _builder.Build(hero: """
            { "attribute": "Attribute.Skill.Stealth", "value": 12.4 },
            { "attribute": "Attribute.Physical.Health", "value": 50.6 },
            { "attribute": "Attribute.Luck", "value": 300 }
            """);
        AttributeSystem attributes = _builder.Attributes;

        attributes.GetValue(_builder.Hero, Stealth).Should().Be(10);
        attributes.GetValue(_builder.Hero, Health).Should().BeApproximately(50.6f, 1e-4f);
        attributes.GetValue(_builder.Hero, new Tag("Attribute.Luck")).Should().Be(300);
        attributes.HasAttribute(_builder.Hero, Stealth).Should().BeTrue();
        attributes.HasAttribute(_builder.Guard, Stealth).Should().BeFalse();
        attributes.GetValue(_builder.Guard, Stealth).Should().Be(0);
    }

    [Fact]
    public void BaseChangesClampAndAreSent()
    {
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 3 }""");
        List<AttributeChangedEvent> changed = _builder.Collect<AttributeChangedEvent>();

        _builder.Attributes.AddToBaseValue(_builder.Hero, Stealth, 4.4f).Should().BeTrue();
        _builder.Attributes.SetBaseValue(_builder.Hero, Stealth, -5).Should().BeTrue();
        _builder.Attributes.AddToBaseValue(_builder.Guard, Stealth, 1).Should().BeFalse();
        _builder.Dispatch();

        _builder.Attributes.GetBaseValue(_builder.Hero, Stealth).Should().Be(0);
        changed.Should().Equal(
            new AttributeChangedEvent(_builder.Hero, Stealth, 3, 7),
            new AttributeChangedEvent(_builder.Hero, Stealth, 7, 0));
    }

    [Fact]
    public void AModifierAddsAndMultipliesGrantsItsTagsAndComesOffAgain()
    {
        _builder.Build(hero: """
            { "attribute": "Attribute.Skill.Stealth", "value": 5 },
            { "attribute": "Attribute.Physical.Health", "value": 80 }
            """);
        ModifierDefinition drunk = _builder.Load<ModifierDefinition>(AttributeSystemTestsBuilder.Drunk);
        AttributeSystem attributes = _builder.Attributes;

        Guid handle = attributes.ApplyModifier(_builder.Hero, drunk);
        float drunkStealth = attributes.GetValue(_builder.Hero, Stealth);
        float drunkHealth = attributes.GetValue(_builder.Hero, Health);
        float baseStealth = attributes.GetBaseValue(_builder.Hero, Stealth);
        bool tagged = _builder.Game.World.Get<OwnedTags>(_builder.Hero).HasMatchingTag(new Tag("Status.Drunk"));
        attributes.RemoveModifier(_builder.Hero, handle).Should().BeTrue();

        drunkStealth.Should().Be(3);
        drunkHealth.Should().Be(40);
        baseStealth.Should().Be(5);
        tagged.Should().BeTrue();
        attributes.GetValue(_builder.Hero, Stealth).Should().Be(5);
        attributes.GetValue(_builder.Hero, Health).Should().Be(80);
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).Tags.Should().BeEmpty();
    }

    [Fact]
    public void AnOverrideWinsAndIsStillClamped()
    {
        _builder.WithFile("Modifiers/Shadow.fvdata", """
            { "type": "modifier", "changes": [ { "attribute": "Attribute.Skill.Stealth", "operation": "Override", "value": 50 } ] }
            """);
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 2 }""");
        ModifierDefinition shadow = _builder.Load<ModifierDefinition>("Modifiers/Shadow.fvdata");
        ModifierDefinition drunk = _builder.Load<ModifierDefinition>(AttributeSystemTestsBuilder.Drunk);

        _builder.Attributes.ApplyModifier(_builder.Hero, shadow);
        _builder.Attributes.ApplyModifier(_builder.Hero, drunk);

        _builder.Attributes.GetValue(_builder.Hero, Stealth).Should().Be(10);
    }

    [Fact]
    public void RemovingAModifierTakesOffEveryInstanceOfIt()
    {
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 5 }""");
        ModifierDefinition drunk = _builder.Load<ModifierDefinition>(AttributeSystemTestsBuilder.Drunk);

        _builder.Attributes.ApplyModifier(_builder.Hero, drunk);
        _builder.Attributes.ApplyModifier(_builder.Hero, drunk);
        float twice = _builder.Attributes.GetValue(_builder.Hero, Stealth);
        int removed = _builder.Attributes.RemoveModifiers(_builder.Hero, drunk);

        twice.Should().Be(1);
        removed.Should().Be(2);
        _builder.Attributes.GetValue(_builder.Hero, Stealth).Should().Be(5);
        _builder.Attributes.GetModifiers(_builder.Hero).Should().BeEmpty();
    }

    [Fact]
    public void TheAttributeConditionComparesTheSubjectAndScoresAtLeast()
    {
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 6 }""", guard: """{ "attribute": "Attribute.Skill.Stealth", "value": 2 }""");
        ConditionContext context = _builder.Context();
        AttributeCondition heroAtLeastFive = new() { Attribute = Stealth, Value = 5 };
        AttributeCondition guardAtLeastFive = new() { Attribute = Stealth, Value = 5, Subject = ContextEntity.Target };
        AttributeCondition heroHealthy = new() { Attribute = Health, Compare = Compare.Less, Value = 100 };

        heroAtLeastFive.Check(context).Should().BeTrue();
        guardAtLeastFive.Check(context).Should().BeFalse();
        heroHealthy.Check(context).Should().BeFalse();
        heroAtLeastFive.TryGetScore(context, out ConditionScore score).Should().BeTrue();
        score.Should().Be(ConditionScore.AtLeast(6, 5));
        heroAtLeastFive.Describe().Should().Be("Stealth >= 5");
    }

    [Fact]
    public void TheAttributeEffectsChangeTheSubject()
    {
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 5 }""", guard: """{ "attribute": "Attribute.Skill.Stealth", "value": 5 }""");
        ConditionContext context = _builder.Context();

        new ModifyAttributeEffect { Attribute = Stealth, Delta = 2 }.Apply(context);
        new SetAttributeEffect { Attribute = Stealth, Value = 1, Subject = ContextEntity.Target }.Apply(context);
        new ApplyModifierEffect { Modifier = AttributeSystemTestsBuilder.Drunk }.Apply(context);
        float drunk = _builder.Attributes.GetValue(_builder.Hero, Stealth);
        new RemoveModifierEffect { Modifier = AttributeSystemTestsBuilder.Drunk }.Apply(context);

        drunk.Should().Be(5);
        _builder.Attributes.GetValue(_builder.Hero, Stealth).Should().Be(7);
        _builder.Attributes.GetValue(_builder.Guard, Stealth).Should().Be(1);
    }

    [Fact]
    public void AttributesAreReadFromDataFiles()
    {
        _builder.WithFile("Rules.fvdata", """
            { "type": "modifier", "changes": [ { "attribute": "Attribute.Skill.Stealth", "value": 1 } ] }
            """);
        _builder.Build(hero: """{ "attribute": "Attribute.Skill.Stealth", "value": 5 }""");
        AttributeDefinition stealth = _builder.Attributes.FindDefinition(Stealth)!;
        ModifierDefinition plain = _builder.Load<ModifierDefinition>("Rules.fvdata");

        stealth.Display.Name.Text.Should().Be("Stealth");
        stealth.Category.Should().Be(new Tag("Attribute.Skill"));
        stealth.Clamp(4.6f).Should().Be(5);
        plain.Changes.Should().ContainSingle().Which.Operation.Should().Be(ModifierOperation.Add);
    }
}
