using FlickerVoid.Engine.Runtime.Services;

namespace FlickerVoid.Engine.Runtime.Tests.Services;

/// <summary>Builds the service registry under test with its starting services.</summary>
internal sealed class ServiceRegistryTestsBuilder
{
    private readonly List<Action<ServiceRegistry>> _registrations = [];

    public ServiceRegistryTestsBuilder WithService<T>(T service)
        where T : class
    {
        _registrations.Add(registry => registry.AddService(service));
        return this;
    }

    public ServiceRegistry Build()
    {
        ServiceRegistry registry = new();
        foreach (Action<ServiceRegistry> register in _registrations)
        {
            register(registry);
        }

        return registry;
    }
}
