using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Saves;

/// <summary>
/// What a save keeps: modules add their states by key, read back in the order they were added, of each <see cref="Saveable"/>
/// entity (a component kept as it is, or state of their own) and of the game as a whole.
/// </summary>
public sealed class SaveStates(World world)
{
    private readonly List<SaveState> _states = [];
    private readonly List<GlobalSaveState> _globals = [];

    public IReadOnlyList<SaveState> States => _states;

    public IReadOnlyList<GlobalSaveState> Globals => _globals;

    /// <summary>Saves game-wide state: what <paramref name="write"/> gives (null for nothing), handed to <paramref name="read"/> when a save holds it.</summary>
    public void AddGlobal<TState>(string key, JsonTypeInfo<TState> info, Func<TState?> write, Action<TState> read)
        where TState : class
    {
        _globals.Add(new GlobalSaveState(key, () => WriteState(Entity.Null, info, _ => write()), node => ReadState(Entity.Null, node, info, (_, state) => read(state))));
    }

    /// <summary>Saves the component as it is; loading sets it back, or removes it from an entity that had none when saved.</summary>
    public void AddComponent<T>(string key, JsonTypeInfo<T> info)
        where T : struct
    {
        _states.Add(new SaveState(key, entity => WriteComponent(entity, info), (entity, node) => ReadComponent(entity, node, info)));
    }

    /// <summary>Saves what <paramref name="write"/> gives (null for nothing) and hands it to <paramref name="read"/> when a save holds it.</summary>
    public void Add<TState>(string key, JsonTypeInfo<TState> info, Func<Entity, TState?> write, Action<Entity, TState> read)
        where TState : class
    {
        _states.Add(new SaveState(key, entity => WriteState(entity, info, write), (entity, node) => ReadState(entity, node, info, read)));
    }

    private static JsonNode? WriteState<TState>(Entity entity, JsonTypeInfo<TState> info, Func<Entity, TState?> write)
        where TState : class
    {
        TState? state = write(entity);
        if (state is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(state, info);
    }

    private static void ReadState<TState>(Entity entity, JsonNode? node, JsonTypeInfo<TState> info, Action<Entity, TState> read)
        where TState : class
    {
        if (node is null)
        {
            return;
        }

        TState? state = node.Deserialize(info);
        if (state is not null)
        {
            read(entity, state);
        }
    }

    private JsonNode? WriteComponent<T>(Entity entity, JsonTypeInfo<T> info)
        where T : struct
    {
        if (!world.TryGet(entity, out T component))
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(component, info);
    }

    private void ReadComponent<T>(Entity entity, JsonNode? node, JsonTypeInfo<T> info)
        where T : struct
    {
        if (node is null)
        {
            if (world.Has<T>(entity))
            {
                world.Remove<T>(entity);
            }

            return;
        }

        T component = node.Deserialize(info);
        if (world.Has<T>(entity))
        {
            world.Set(entity, component);
        }
        else
        {
            world.Add(entity, component);
        }
    }
}
