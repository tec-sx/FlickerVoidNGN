using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Scenes;

public sealed partial class EditorScene
{
    /// <summary>Gives the entity the component, replacing the one it has.</summary>
    public void SetComponent<T>(Entity entity, in T component)
        where T : struct
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        SceneComponent? registered = FindComponent(typeof(T));
        JsonElement? before = WriteComponent(entity, registered);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        lifecycle.StopComponent(world, entity, typeof(T));
        world.Set(entity, component);
        lifecycle.StartComponent(world, entity, typeof(T));
        if (registered is not null)
        {
            RecordComponent("Edit", entity, registered, before);
        }

        MarkEdited();
    }

    /// <summary>Gives the entity the component registered for scene files, from a boxed value of its type.</summary>
    public void SetComponent(Entity entity, SceneComponent component, object value)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        JsonElement? before = WriteComponent(entity, component);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        Type type = component.TypeInfo.Type;
        lifecycle.StopComponent(world, entity, type);
        component.Set(world, entity, value);
        lifecycle.StartComponent(world, entity, type);
        RecordComponent("Edit", entity, component, before);
        MarkEdited();
    }

    /// <summary>Gives the entity the component with its defaults, unless it has it already.</summary>
    public void AddComponent(Entity entity, SceneComponent component)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || component.Has(world, entity))
        {
            return;
        }

        using JsonDocument defaults = JsonDocument.Parse("{}");
        component.Read(world, entity, defaults.RootElement);
        Type type = component.TypeInfo.Type;
        Game.Spawner.Lifecycle.StartComponent(world, entity, type);
        RecordComponent("Add", entity, component, null);
        MarkEdited();
    }

    /// <summary>Gives the entity the component as the JSON describes it, replacing the one it has.</summary>
    public void ReadComponent(Entity entity, SceneComponent component, JsonElement data)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        JsonElement? before = WriteComponent(entity, component);
        EntityLifecycle lifecycle = Game.Spawner.Lifecycle;
        Type type = component.TypeInfo.Type;
        if (before is not null)
        {
            lifecycle.StopComponent(world, entity, type);
            component.Remove(world, entity);
        }

        component.Read(world, entity, data);
        lifecycle.StartComponent(world, entity, type);
        RecordComponent("Edit", entity, component, before);
        MarkEdited();
    }

    public void RemoveComponent(Entity entity, SceneComponent component)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !component.Has(world, entity))
        {
            return;
        }

        JsonElement? before = WriteComponent(entity, component);
        Type type = component.TypeInfo.Type;
        Game.Spawner.Lifecycle.StopComponent(world, entity, type);
        component.Remove(world, entity);
        RecordComponent("Remove", entity, component, before);
        MarkEdited();
    }

    private SceneComponent? FindComponent(Type type)
    {
        foreach (SceneComponent component in Game.Spawner.Components)
        {
            if (component.TypeInfo.Type == type)
            {
                return component;
            }
        }

        return null;
    }

    private JsonElement? WriteComponent(Entity entity, SceneComponent? component)
    {
        if (History.IsApplying || component is null || !component.Has(Game.World, entity))
        {
            return null;
        }

        return component.Write(Game.World, entity);
    }

    private void RecordComponent(string verb, Entity entity, SceneComponent component, JsonElement? before)
    {
        World world = Game.World;
        if (History.IsApplying || !world.TryGet(entity, out EntityName entityName))
        {
            return;
        }

        JsonElement? after = null;
        if (component.Has(world, entity))
        {
            after = component.Write(world, entity);
        }

        ComponentStep step = new($"{verb} {entityName.Value}", this, entityName.Value, component, before, after);
        History.Record(step);
    }
}
