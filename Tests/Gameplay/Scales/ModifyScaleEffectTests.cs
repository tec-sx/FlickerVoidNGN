using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Scales;

namespace FlickerVoid.Engine.Gameplay.Tests.Scales;

public class ModifyScaleEffectTests
{
    private readonly FactDatabase _facts = new FactDatabaseTestsBuilder().WithRange(TestTags.B, 0, 100).Build();
    private readonly ScaleDefinition _fame = new ScaleDefinitionTestsBuilder().WithFact(TestTags.B).WithName("Fame").Build();
    private readonly ModifyScaleEffectTestsBuilder _builder;

    public ModifyScaleEffectTests()
    {
        _builder = new ModifyScaleEffectTestsBuilder(_fame);
    }

    [Fact]
    public void ModifiesScaleWithinItsRange()
    {
        ModifyScaleEffect effect = _builder.WithDelta(120).Build();
        ConditionContext context = new(_facts);

        effect.Apply(context);

        int fame = _fame.GetValue(_facts);
        fame.Should().Be(100);
    }

    [Fact]
    public void DescribesWithScaleNameAndDelta()
    {
        ModifyScaleEffect effect = _builder.WithDelta(85).Build();

        string text = effect.Describe();

        text.Should().Be("Fame 85");
    }
}
