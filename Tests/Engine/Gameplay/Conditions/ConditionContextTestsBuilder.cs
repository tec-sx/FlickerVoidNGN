using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;
using FlickerVoid.Engine.Gameplay.Facts;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

/// <summary>Builds the condition context under test on substitute facts.</summary>
internal sealed class ConditionContextTestsBuilder
{
    private readonly IFactDatabase _facts = Substitute.For<IFactDatabase>();
    private IEntityView? _instigator;
    private IEntityView? _target;
    private IServiceProvider? _services;

    public ConditionContextTestsBuilder WithInstigator(IEntityView instigator)
    {
        _instigator = instigator;
        return this;
    }

    public ConditionContextTestsBuilder WithTarget(IEntityView target)
    {
        _target = target;
        return this;
    }

    public ConditionContextTestsBuilder WithServices(IServiceProvider services)
    {
        _services = services;
        return this;
    }

    public ConditionContext Build() => new(_facts) { Instigator = _instigator, Target = _target, Services = _services };
}
