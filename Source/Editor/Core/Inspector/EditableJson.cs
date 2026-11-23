using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// Copies nested values through their metadata, so an edit writes a new object or list back rather than changing the one the
/// world (and the undo step taken before the edit) still holds.
/// </summary>
public static class EditableJson
{
    /// <summary>A deep copy of the value, of the type it is written as (polymorphic values keep their kind).</summary>
    public static object? Copy(object? value, Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo typeInfo = options.GetTypeInfo(type);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    /// <summary>The items of a list or array value; none when it is null.</summary>
    public static List<object?> ReadItems(IEditableValue list)
    {
        List<object?> items = [];
        if (list.Get() is IEnumerable values)
        {
            foreach (object? item in values)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>Writes a new list or array of the value's type holding copies of the items.</summary>
    public static void WriteItems(IEditableValue list, IReadOnlyList<object?> items)
    {
        JsonSerializerOptions options = list.Options!;
        JsonTypeInfo listType = options.GetTypeInfo(list.Type);
        JsonTypeInfo elementType = options.GetTypeInfo(listType.ElementType!);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartArray();
            foreach (object? item in items)
            {
                JsonSerializer.Serialize(writer, item, elementType);
            }

            writer.WriteEndArray();
        }

        byte[] json = stream.ToArray();
        object? written = JsonSerializer.Deserialize(json, listType);
        list.Set(written);
    }
}
