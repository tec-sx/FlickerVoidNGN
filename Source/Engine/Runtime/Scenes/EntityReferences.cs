using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>The names a component's JSON holds in its <see cref="EntityReferenceAttribute"/> properties: read for checks, rewritten for renames.</summary>
public static class EntityReferences
{
    /// <summary>Whether any of the type's properties names an entity.</summary>
    public static bool HasAny(JsonTypeInfo typeInfo)
    {
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (IsReference(property))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The entity names the component's JSON holds, empty ones left out.</summary>
    public static IReadOnlyList<string> Find(JsonTypeInfo typeInfo, JsonElement data)
    {
        List<string> names = [];
        if (data.ValueKind != JsonValueKind.Object)
        {
            return names;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (IsReference(property) && data.TryGetProperty(property.Name, out JsonElement value) && value.GetString() is { Length: > 0 } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>The component's JSON with every reference to <paramref name="from"/> naming <paramref name="to"/>; null when none named it.</summary>
    public static JsonElement? Rename(JsonTypeInfo typeInfo, JsonElement data, string from, string to)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonObject? renamed = null;
        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (!IsReference(property) || !data.TryGetProperty(property.Name, out JsonElement value) || value.ValueKind != JsonValueKind.String || value.GetString() != from)
            {
                continue;
            }

            renamed ??= JsonObject.Create(data);
            renamed![property.Name] = to;
        }

        if (renamed is null)
        {
            return null;
        }

        string text = renamed.ToJsonString();
        using JsonDocument document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static bool IsReference(JsonPropertyInfo property)
    {
        return property.PropertyType == typeof(string) && property.AttributeProvider is { } provider && provider.IsDefined(typeof(EntityReferenceAttribute), inherit: true);
    }
}
