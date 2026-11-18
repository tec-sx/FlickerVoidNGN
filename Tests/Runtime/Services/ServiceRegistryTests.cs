using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Runtime.Tests.Services;

public class ServiceRegistryTests
{
    private readonly ServiceRegistry _services = new();

    [Fact]
    public void FindsServicesByType()
    {
        Clock clock = new();
        _services.AddService(clock);

        Clock? byGeneric = _services.GetService<Clock>();
        IServiceProvider provider = _services;
        object? byType = provider.GetService(typeof(Clock));

        Assert.Same(clock, byGeneric);
        Assert.Same(clock, byType);
    }

    [Fact]
    public void ReturnsNullForMissingServices()
    {
        Clock? missing = _services.GetService<Clock>();

        Assert.Null(missing);
    }

    [Fact]
    public void RefusesASecondServiceOfTheSameType()
    {
        _services.AddService(new Clock());

        Assert.Throws<ArgumentException>(() => _services.AddService(new Clock()));
    }

    [Fact]
    public void RemovesServices()
    {
        _services.AddService(new Clock());

        bool removed = _services.RemoveService<Clock>();
        bool removedAgain = _services.RemoveService<Clock>();
        Clock? afterRemove = _services.GetService<Clock>();

        Assert.True(removed);
        Assert.False(removedAgain);
        Assert.Null(afterRemove);
    }

    private sealed class Clock;
}
