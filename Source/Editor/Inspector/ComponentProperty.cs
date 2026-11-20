using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>One property of a component on an entity, read and written through the component's source-generated metadata.</summary>
public sealed class ComponentProperty
{
    private readonly World _world;
    private readonly Entity _entity;
    private readonly SceneComponent _component;
    private readonly JsonPropertyInfo _property;

    public ComponentProperty(World world, Entity entity, SceneComponent component, JsonPropertyInfo property)
    {
        _world = world;
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
        ICustomAttributeProvider? provider = _property.AttributeProvider;
        if (provider is null)
        {
            return null;
        }

        object[] found = provider.GetCustomAttributes(typeof(TAttribute), inherit: true);
        if (found.Length == 0)
        {
            return null;
        }

        return (TAttribute)found[0];
    }

    public object? Get()
    {
        object component = _component.Get(_world, _entity);
        return _property.Get!(component);
    }

    /// <summary>Sets the property on a copy of the component and gives the copy back to the entity.</summary>
    public void Set(object? value)
    {
        object component = _component.Get(_world, _entity);
        _property.Set!(component, value);
        _component.Set(_world, _entity, component);
    }

    /// <summary>The value as it would be written in a scene file.</summary>
    public string ToJson(object? value)
    {
        JsonTypeInfo typeInfo = _property.Options.GetTypeInfo(Type);
        return JsonSerializer.Serialize(value, typeInfo);
    }
}
