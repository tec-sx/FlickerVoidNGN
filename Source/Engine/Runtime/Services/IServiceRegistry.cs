namespace FlickerVoid.Engine.Runtime.Services;

/// <summary>The game's services by type, such as the logger factory and the physics simulation.</summary>
public interface IServiceRegistry : IServiceProvider
{
    T? GetService<T>()
        where T : class;

    /// <exception cref="ArgumentException">A service is already registered under <typeparamref name="T"/>.</exception>
    void AddService<T>(T service)
        where T : class;

    /// <summary>Removes the service registered under <typeparamref name="T"/>; false when there is none.</summary>
    bool RemoveService<T>()
        where T : class;
}
