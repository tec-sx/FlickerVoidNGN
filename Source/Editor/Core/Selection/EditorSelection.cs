using Arch.Core;

namespace FlickerVoid.Editor.Core.Selection;

/// <summary>
/// The entities the editor works on: <see cref="Entities"/>, such as everything in an outline folder, and among them
/// <see cref="Selected"/>, the one the inspector shows and the gizmo sits on. Raises <see cref="Changed"/> when they change.
/// </summary>
public sealed class EditorSelection
{
    private Entity[] _entities = [];

    public Entity? Selected { get; private set; }

    public IReadOnlyList<Entity> Entities => _entities;

    public event Action? Changed;

    public bool Contains(Entity entity) => _entities.Contains(entity);

    /// <summary>Selects one entity, or nothing.</summary>
    public void Select(Entity? entity)
    {
        Entity[] entities = [];
        if (entity is { } one)
        {
            entities = [one];
        }

        SelectAll(entities, entity);
    }

    /// <summary>Selects the entities, with <paramref name="primary"/> (by default the last of them) for the inspector and gizmo.</summary>
    public void SelectAll(IEnumerable<Entity> entities, Entity? primary = null)
    {
        Entity[] distinct = [.. entities.Distinct()];
        Entity? chosen = primary;
        if (chosen is null || !distinct.Contains(chosen.Value))
        {
            chosen = distinct.Length > 0 ? distinct[^1] : null;
        }

        if (Selected == chosen && _entities.SequenceEqual(distinct))
        {
            return;
        }

        _entities = distinct;
        Selected = chosen;
        Changed?.Invoke();
    }

    /// <summary>Leaves the entity out of the selection, as when it is destroyed.</summary>
    public void Deselect(Entity entity)
    {
        if (!Contains(entity))
        {
            return;
        }

        Entity[] rest = [.. _entities.Where(selected => selected != entity)];
        Entity? primary = Selected == entity ? null : Selected;
        SelectAll(rest, primary);
    }
}
