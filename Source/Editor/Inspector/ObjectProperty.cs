using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>One property of a settings object, such as a lighting mood, read and written in place through its source-generated metadata.</summary>
public sealed class ObjectProperty(object target, JsonPropertyInfo property) : IEditableValue
{
    public string Label { get; } = DisplayNames.FromIdentifier(property.Name);

    public Type Type => property.PropertyType;

    public bool CanWrite => property.Set is not null;

    public bool AcceptsNull => property.IsSetNullable;

    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return PropertyAttributes.Find<TAttribute>(property);
    }

    public object? Get() => property.Get!(target);

    public void Set(object? value) => property.Set!(target, value);

    public string ToJson(object? value)
    {
        JsonTypeInfo typeInfo = property.Options.GetTypeInfo(Type);
        return JsonSerializer.Serialize(value, typeInfo);
    }
}
