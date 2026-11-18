using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

public class EffectListTests
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private readonly ConditionContext _context;

    public EffectListTests()
    {
        _context = new ConditionContext(_facts);
    }

    [Fact]
    public void AppliesEffectsInOrder()
    {
        Effect first = Substitute.For<Effect>();
        Effect second = Substitute.For<Effect>();
        EffectList effects = new EffectListTestsBuilder().With(first).With(second).Build();

        effects.Apply(_context);

        Received.InOrder(() =>
        {
            first.Apply(_context);
            second.Apply(_context);
        });
    }
}
