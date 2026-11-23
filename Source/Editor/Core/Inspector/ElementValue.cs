using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>One item of a list or array value, such as a shot's key; written by writing a new list with the item replaced.</summary>
public sealed class ElementValue(IEditableValue list, Type elementType, int index) : IEditableValue
{
    public string Label { get; } = $"{index + 1}";

    public Type Type => elementType;

    public bool CanWrite => list.CanWrite;

    public bool AcceptsNull => false;

    public JsonSerializerOptions Options => list.Options!;

    /// <summary>The list's attributes, so a list of file names picks content files.</summary>
    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return list.GetAttribute<TAttribute>();
    }

    public object? Get()
    {
        if (list.Get() is not IList items || index >= items.Count)
        {
            return null;
        }

        return items[index];
    }

    public void Set(object? value)
    {
        List<object?> items = EditableJson.ReadItems(list);
        if (index >= items.Count)
        {
            return;
        }

        items[index] = value;
        EditableJson.WriteItems(list, items);
    }

    public string ToJson(object? value)
    {
        JsonTypeInfo typeInfo = Options.GetTypeInfo(Type);
        return JsonSerializer.Serialize(value, typeInfo);
    }
}
