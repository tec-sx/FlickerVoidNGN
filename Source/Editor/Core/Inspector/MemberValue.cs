using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// One property of a nested object, such as a camera cue's transition's duration: read from the owner's value, and written by
/// setting it on a copy of that value and writing the copy through the owner.
/// </summary>
public sealed class MemberValue(IEditableValue owner, JsonPropertyInfo property) : IEditableValue
{
    public string Label { get; } = DisplayNames.FromIdentifier(property.Name);

    public Type Type => property.PropertyType;

    public bool CanWrite => owner.CanWrite && property.Set is not null;

    public bool AcceptsNull => property.IsSetNullable;

    public JsonSerializerOptions Options => property.Options;

    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return PropertyAttributes.Find<TAttribute>(property);
    }

    public object? Get()
    {
        object? target = owner.Get();
        if (target is null)
        {
            return null;
        }

        return property.Get!(target);
    }

    public void Set(object? value)
    {
        object? target = owner.Get();
        if (target is null)
        {
            return;
        }

        object copy = EditableJson.Copy(target, owner.Type, owner.Options!)!;
        property.Set!(copy, value);
        owner.Set(copy);
    }

    public string ToJson(object? value)
    {
        JsonTypeInfo typeInfo = property.Options.GetTypeInfo(Type);
        return JsonSerializer.Serialize(value, typeInfo);
    }
}
