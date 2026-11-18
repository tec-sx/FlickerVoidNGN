using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Entities;

namespace FlickerVoid.Engine.Gameplay.Tests.Conditions;

public class ConditionContextTests
{
    private readonly IEntityView _player = Substitute.For<IEntityView>();
    private readonly IEntityView _guard = Substitute.For<IEntityView>();
    private readonly ConditionContextTestsBuilder _builder = new();

    [Theory]
    [InlineData(ContextEntity.Instigator, true)]
    [InlineData(ContextEntity.Target, false)]
    public void PicksTheInstigatorOrTheTarget(ContextEntity which, bool expectPlayer)
    {
        ConditionContext context = _builder.WithInstigator(_player).WithTarget(_guard).Build();

        IEntityView? entity = context.GetEntity(which);

        IEntityView expected = _guard;
        if (expectPlayer)
        {
            expected = _player;
        }

        entity.Should().BeSameAs(expected);
    }

    [Fact]
    public void FindsServicesByType()
    {
        Clock clock = new();
        IServiceProvider services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(Clock)).Returns(clock);
        ConditionContext context = _builder.WithServices(services).Build();

        Clock? found = context.GetService<Clock>();

        found.Should().BeSameAs(clock);
    }

    [Fact]
    public void HasNoServicesUnlessGivenThem()
    {
        ConditionContext context = _builder.Build();

        Clock? found = context.GetService<Clock>();

        found.Should().BeNull();
    }

    private sealed class Clock;
}
