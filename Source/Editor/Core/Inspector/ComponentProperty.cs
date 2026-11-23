using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// One property of a component on an entity, read and written through the component's source-generated metadata; writes go
/// through the edited scene.
/// </summary>
public sealed class ComponentProperty : IEditableValue
{
    private readonly EditorScene _scene;
    private readonly Entity _entity;
    private readonly SceneComponent _component;
    private readonly JsonPropertyInfo _property;

    public ComponentProperty(EditorScene scene, Entity entity, SceneComponent component, JsonPropertyInfo property)
    {
        _scene = scene;
        _entity = entity;
        _component = component;
        _property = property;
        Label = DisplayNames.FromIdentifier(property.Name);
    }

    public string Label { get; }

    public Type Type => _property.PropertyType;

    public bool CanWrite => _property.Set is not null;

    /// <summary>Whether the property may be set to null, such as an optional file name.</summary>
    public bool AcceptsNull => _property.IsSetNullable;

    /// <summary>The attribute the component declares on the property, if any.</summary>
    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return PropertyAttributes.Find<TAttribute>(_property);
    }

    public JsonSerializerOptions Options => _property.Options;

    public object? Get()
    {
        World world = _scene.Game.World;
        object component = _component.Get(world, _entity);
        return _property.Get!(component);
    }

    /// <summary>Sets the property on a copy of the component and gives the copy back to the entity.</summary>
    public void Set(object? value)
    {
        World world = _scene.Game.World;
        object component = _component.Get(world, _entity);
        _property.Set!(component, value);
        _scene.SetComponent(_entity, _component, component);
    }

    /// <summary>The value as it would be written in a scene file.</summary>
    public string ToJson(object? value)
    {
        JsonTypeInfo typeInfo = _property.Options.GetTypeInfo(Type);
        return JsonSerializer.Serialize(value, typeInfo);
    }
}
