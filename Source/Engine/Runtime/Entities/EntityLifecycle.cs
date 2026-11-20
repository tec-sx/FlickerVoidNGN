using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>
/// Runs the hooks systems add for their components as entities start and stop. Starting attaches an entity (in the editor and the
/// game, for looks that come from data) and, while the game plays, activates it; stopping deactivates and detaches it. Hooks run in
/// the order they were added, and in reverse when stopping. An entity's phase is kept on it as the <see cref="Attached"/> and
/// <see cref="Active"/> tags.
/// </summary>
public sealed class EntityLifecycle
{
    private static readonly QueryDescription AttachedEntities = new QueryDescription().WithAll<Attached>().WithNone<Active>();
    private static readonly QueryDescription ActiveEntities = new QueryDescription().WithAll<Active>();
    private static readonly QueryDescription StartedEntities = new QueryDescription().WithAll<Attached>();

    private readonly List<Hook> _attach = [];
    private readonly List<Hook> _activate = [];
    private readonly List<Hook> _deactivate = [];
    private readonly List<Hook> _detach = [];

    public EntityLifecycle()
    {
        ArrayRegistry.Add<Attached>();
        ArrayRegistry.Add<Active>();
    }

    /// <summary>Whether starting an entity also activates it: true in the game once it plays, false in the editor.</summary>
    public bool IsPlaying { get; private set; }

    public void OnAttach<T>(ComponentHook<T> hook)
        where T : struct
    {
        _attach.Add(Hook.Create(hook));
    }

    public void OnActivate<T>(ComponentHook<T> hook)
        where T : struct
    {
        _activate.Add(Hook.Create(hook));
    }

    public void OnDeactivate<T>(ComponentHook<T> hook)
        where T : struct
    {
        _deactivate.Add(Hook.Create(hook));
    }

    public void OnDetach<T>(ComponentHook<T> hook)
        where T : struct
    {
        _detach.Add(Hook.Create(hook));
    }

    /// <summary>Activates every attached entity of the world, and every entity started from now on.</summary>
    public void Play(World world)
    {
        IsPlaying = true;
        List<Entity> attached = [];
        world.Query(in AttachedEntities, attached.Add);
        foreach (Entity entity in attached)
        {
            Activate(world, entity);
        }
    }

    /// <summary>Stops every started entity of the world, as when the game closes.</summary>
    public void StopAll(World world)
    {
        List<Entity> active = [];
        world.Query(in ActiveEntities, active.Add);
        foreach (Entity entity in active)
        {
            Deactivate(world, entity);
        }

        List<Entity> attached = [];
        world.Query(in StartedEntities, attached.Add);
        foreach (Entity entity in attached)
        {
            Detach(world, entity);
        }
    }

    /// <summary>Attaches the entity and, while playing, activates it; does nothing for a phase it is already in.</summary>
    public void Start(World world, Entity entity)
    {
        if (!world.Has<Attached>(entity))
        {
            Run(_attach, world, entity, reverse: false);
            world.Add<Attached>(entity);
        }

        if (IsPlaying && !world.Has<Active>(entity))
        {
            Activate(world, entity);
        }
    }

    /// <summary>Deactivates and detaches the entity, for the phases it is in.</summary>
    public void Stop(World world, Entity entity)
    {
        if (world.Has<Active>(entity))
        {
            Deactivate(world, entity);
        }

        if (world.Has<Attached>(entity))
        {
            Detach(world, entity);
        }
    }

    /// <summary>Stops the entity, then destroys it.</summary>
    public void Destroy(World world, Entity entity)
    {
        Stop(world, entity);
        world.Destroy(entity);
    }

    /// <summary>Runs one component's attach and activate hooks for the phases a started entity is in, after the component was added or changed.</summary>
    public void StartComponent(World world, Entity entity, Type component)
    {
        if (world.Has<Attached>(entity))
        {
            Run(_attach, world, entity, component, reverse: false);
        }

        if (world.Has<Active>(entity))
        {
            Run(_activate, world, entity, component, reverse: false);
        }
    }

    /// <summary>Runs one component's deactivate and detach hooks for the phases a started entity is in, before the component is removed or changed.</summary>
    public void StopComponent(World world, Entity entity, Type component)
    {
        if (world.Has<Active>(entity))
        {
            Run(_deactivate, world, entity, component, reverse: true);
        }

        if (world.Has<Attached>(entity))
        {
            Run(_detach, world, entity, component, reverse: true);
        }
    }

    private void Activate(World world, Entity entity)
    {
        Run(_activate, world, entity, reverse: false);
        world.Add<Active>(entity);
    }

    private void Deactivate(World world, Entity entity)
    {
        Run(_deactivate, world, entity, reverse: true);
        world.Remove<Active>(entity);
    }

    private void Detach(World world, Entity entity)
    {
        Run(_detach, world, entity, reverse: true);
        world.Remove<Attached>(entity);
    }

    private static void Run(List<Hook> hooks, World world, Entity entity, bool reverse)
    {
        Run(hooks, world, entity, component: null, reverse);
    }

    private static void Run(List<Hook> hooks, World world, Entity entity, Type? component, bool reverse)
    {
        for (int step = 0; step < hooks.Count; step++)
        {
            int index = step;
            if (reverse)
            {
                index = hooks.Count - 1 - step;
            }

            Hook hook = hooks[index];
            if (component is not null && hook.Component != component)
            {
                continue;
            }

            hook.Run(world, entity);
        }
    }

    /// <summary>A hook for one component type, run only on entities that have the component.</summary>
    private sealed class Hook
    {
        private readonly Action<World, Entity> _run;

        private Hook(Type component, Action<World, Entity> run)
        {
            Component = component;
            _run = run;
        }

        public Type Component { get; }

        public static Hook Create<T>(ComponentHook<T> hook)
            where T : struct
        {
            return new Hook(typeof(T), (world, entity) =>
            {
                if (world.Has<T>(entity))
                {
                    hook(entity, ref world.Get<T>(entity));
                }
            });
        }

        public void Run(World world, Entity entity) => _run(world, entity);
    }
}
