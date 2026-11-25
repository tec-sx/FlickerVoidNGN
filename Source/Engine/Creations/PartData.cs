using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// One part as its kind describes it: an id unique in the creation, where it stands in the space of the entity placed in the level
/// (its pivot), and the components the kind gives it, such as its mesh.
/// </summary>
public sealed class PartData
{
    private readonly List<PartComponent> _components = [];

    public PartData(string id, Transform place)
    {
        Id = id;
        Place = place;
    }

    public string Id { get; }

    public Transform Place { get; }

    internal IReadOnlyList<PartComponent> Components => _components;

    /// <summary>Gives the part a component, replacing one of the same type; returns the part, so components can follow one another.</summary>
    public PartData With<T>(in T component)
        where T : struct
    {
        _components.RemoveAll(existing => existing.Type == typeof(T));
        PartComponentOf<T> added = new(component);
        _components.Add(added);
        return this;
    }

    internal PartComponent? Find(Type type)
    {
        return _components.Find(component => component.Type == type);
    }
}
