using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Scenes;

public sealed partial class EditorScene
{
    /// <summary>Files the entity under an outline folder, such as <c>Lights/Hall</c>, or at the top for null or empty.</summary>
    public void SetFolder(Entity entity, string? path)
    {
        World world = Game.World;
        if (!world.IsAlive(entity))
        {
            return;
        }

        string? current = world.TryGet(entity, out EntityFolder folder) ? folder.Path : null;
        string? wanted = string.IsNullOrEmpty(path) ? null : path;
        if (current == wanted)
        {
            return;
        }

        if (!History.IsApplying && world.TryGet(entity, out EntityName entityName))
        {
            FolderStep step = new($"Move {entityName.Value} to folder", this, entityName.Value, current, wanted);
            History.Record(step);
        }

        if (wanted is null)
        {
            world.Remove<EntityFolder>(entity);
        }
        else if (current is null)
        {
            world.Add(entity, new EntityFolder(wanted));
        }
        else
        {
            world.Set(entity, new EntityFolder(wanted));
        }

        EntitiesChanged?.Invoke();
        MarkEdited();
    }

    /// <summary>
    /// Nests the entity under <paramref name="parent"/>, or takes it off its parent for null, keeping where it stands; false when the
    /// parent is the entity itself or one of its descendants, or the entities have no names to link them by.
    /// </summary>
    public bool SetParent(Entity entity, Entity? parent)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !world.TryGet(entity, out EntityName entityName))
        {
            return false;
        }

        string? wanted = null;
        if (parent is { } parentEntity)
        {
            if (parentEntity == entity || GetDescendants(entity).Contains(parentEntity) || !world.TryGet(parentEntity, out EntityName parentName))
            {
                return false;
            }

            wanted = parentName.Value;
        }

        string? current = world.TryGet(entity, out EntityParent existing) ? existing.Name : null;
        if (current == wanted)
        {
            return true;
        }

        if (!History.IsApplying)
        {
            string verb = wanted is null ? $"Take {entityName.Value} off {current}" : $"Nest {entityName.Value} under {wanted}";
            ParentStep step = new(verb, this, entityName.Value, current, wanted);
            History.Record(step);
        }

        if (wanted is null)
        {
            world.Remove<EntityParent>(entity);
        }
        else if (current is null)
        {
            world.Add(entity, new EntityParent(wanted));
        }
        else
        {
            world.Set(entity, new EntityParent(wanted));
        }

        EntitiesChanged?.Invoke();
        MarkEdited();
        return true;
    }

    /// <summary>Nests every entity under <paramref name="parent"/>, or takes them off their parents for null, as one undo step.</summary>
    public void SetParent(IEnumerable<Entity> entities, Entity? parent)
    {
        string name = parent is { } nest && Game.World.TryGet(nest, out EntityName parentName) ? $"Nest under {parentName.Value}" : "Take off parent";
        History.BeginGroup(name);
        foreach (Entity entity in entities.ToList())
        {
            SetParent(entity, parent);
        }

        History.EndGroup();
    }

    /// <summary>The entity of the open scene this one is nested under, or null at the top or when its parent is gone.</summary>
    public Entity? GetParent(Entity entity)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !world.TryGet(entity, out EntityParent parent))
        {
            return null;
        }

        Entity? found = Find(parent.Name);
        if (found == entity)
        {
            return null;
        }

        return found;
    }

    /// <summary>The entities nested directly under this one, in the file's order.</summary>
    public IReadOnlyList<Entity> GetChildren(Entity entity)
    {
        World world = Game.World;
        if (!world.IsAlive(entity) || !world.TryGet(entity, out EntityName name))
        {
            return [];
        }

        List<Entity> children = [];
        foreach (Entity candidate in Entities)
        {
            if (candidate != entity && world.TryGet(candidate, out EntityParent parent) && parent.Name == name.Value)
            {
                children.Add(candidate);
            }
        }

        return children;
    }

    /// <summary>Every entity nested under this one, at any depth, parents before their children.</summary>
    public IReadOnlyList<Entity> GetDescendants(Entity entity)
    {
        List<Entity> descendants = [];
        HashSet<Entity> seen = [entity];
        Queue<Entity> waiting = new();
        waiting.Enqueue(entity);
        while (waiting.TryDequeue(out Entity current))
        {
            foreach (Entity child in GetChildren(current))
            {
                if (seen.Add(child))
                {
                    descendants.Add(child);
                    waiting.Enqueue(child);
                }
            }
        }

        return descendants;
    }

    /// <summary>Raised when entities are hidden or shown in the editor's views.</summary>
    public event Action? VisibilityChanged;

    /// <summary>Whether the entity is hidden in the editor's views.</summary>
    public bool IsHidden(Entity entity) => Game.World.IsAlive(entity) && Game.World.Has<Hidden>(entity);

    /// <summary>
    /// Hides the entities and everything nested under them in the editor's views, with the parts of creations, or shows them again;
    /// only the editor's views change, so it is neither saved nor undone, and the game shows them.
    /// </summary>
    public void SetHidden(IEnumerable<Entity> entities, bool hidden)
    {
        World world = Game.World;
        CreationParts? parts = Game.Services.GetService<CreationParts>();
        foreach (Entity entity in entities.ToList())
        {
            if (!world.IsAlive(entity))
            {
                continue;
            }

            IReadOnlyList<Entity> descendants = GetDescendants(entity);
            List<Entity> tree = [entity, .. descendants];
            foreach (Entity member in tree)
            {
                SetHidden(world, member, hidden);
                IReadOnlyList<Entity> memberParts = parts?.FindAll(member) ?? [];
                foreach (Entity part in memberParts)
                {
                    SetHidden(world, part, hidden);
                }
            }
        }

        VisibilityChanged?.Invoke();
    }

    /// <summary>Shows every entity of the open scene again.</summary>
    public void ShowAll()
    {
        SetHidden(Entities, hidden: false);
    }

    private static void SetHidden(World world, Entity entity, bool hidden)
    {
        bool has = world.Has<Hidden>(entity);
        if (hidden && !has)
        {
            world.Add(entity, default(Hidden));
        }
        else if (!hidden && has)
        {
            world.Remove<Hidden>(entity);
        }
    }
}
