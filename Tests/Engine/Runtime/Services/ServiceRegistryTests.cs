using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Runtime.Tests.Services;

public class ServiceRegistryTests
{
    private readonly ServiceRegistryTestsBuilder _builder = new();

    [Fact]
    public void FindsServicesByType()
    {
        Clock clock = new();
        ServiceRegistry services = _builder.WithService(clock).Build();

        Clock? byGeneric = services.GetService<Clock>();
        IServiceProvider provider = services;
        object? byType = provider.GetService(typeof(Clock));

        byGeneric.Should().BeSameAs(clock);
        byType.Should().BeSameAs(clock);
    }

    [Fact]
    public void ReturnsNullForMissingServices()
    {
        ServiceRegistry services = _builder.Build();

        Clock? missing = services.GetService<Clock>();

        missing.Should().BeNull();
    }

    [Fact]
    public void RefusesASecondServiceOfTheSameType()
    {
        Clock second = new();
        ServiceRegistry services = _builder.WithService(new Clock()).Build();

        Action add = () => services.AddService(second);

        add.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemovesServices()
    {
        ServiceRegistry services = _builder.WithService(new Clock()).Build();

        bool removed = services.RemoveService<Clock>();
        bool removedAgain = services.RemoveService<Clock>();

        Clock? afterRemove = services.GetService<Clock>();
        removed.Should().BeTrue();
        removedAgain.Should().BeFalse();
        afterRemove.Should().BeNull();
    }

    private sealed class Clock;
}
