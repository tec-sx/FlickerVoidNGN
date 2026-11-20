using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

/// <summary>Builds the lifecycle under test over its own world, with hooks on transforms and names that record their calls.</summary>
internal sealed class EntityLifecycleTestsBuilder : IDisposable
{
    private bool _recording;
    private bool _playing;

    public World World { get; } = World.Create();

    public List<string> Calls { get; } = [];

    public EntityLifecycleTestsBuilder WithRecordingHooks()
    {
        _recording = true;
        return this;
    }

    public EntityLifecycleTestsBuilder Playing()
    {
        _playing = true;
        return this;
    }

    public EntityLifecycle Build()
    {
        EntityLifecycle lifecycle = new();
        if (_recording)
        {
            Record<Transform>(lifecycle);
            Record<EntityName>(lifecycle);
        }

        if (_playing)
        {
            lifecycle.Play(World);
        }

        return lifecycle;
    }

    public Entity CreateEntity(Transform transform)
    {
        return World.Create(transform);
    }

    public Entity CreateEntity(EntityName name)
    {
        return World.Create(name);
    }

    public Entity CreateEntity(Transform transform, EntityName name)
    {
        return World.Create(transform, name);
    }

    public void Dispose() => World.Destroy(World);

    private void Record<T>(EntityLifecycle lifecycle)
        where T : struct
    {
        string name = typeof(T).Name;
        lifecycle.OnAttach((Entity _, ref T _) => Calls.Add($"attach {name}"));
        lifecycle.OnActivate((Entity _, ref T _) => Calls.Add($"activate {name}"));
        lifecycle.OnDeactivate((Entity _, ref T _) => Calls.Add($"deactivate {name}"));
        lifecycle.OnDetach((Entity _, ref T _) => Calls.Add($"detach {name}"));
    }
}
