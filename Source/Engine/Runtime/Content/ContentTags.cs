using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// The tags a content file holds, found by the types the game reads it with, as the content check finds the files it names: only
/// values of type <see cref="Tag"/> count, so a name or a text's key that looks like a tag is never touched. Renames and removals
/// give the file's new contents, or null when the file holds none of the tags.
/// </summary>
public sealed class ContentTags(Game game)
{
    private static readonly JsonDocumentOptions ParseOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Every tag the file holds, in the order they appear, as often as they appear.</summary>
    public IReadOnlyList<string> Find(string path, byte[] json)
    {
        List<string> found = [];
        Walk(path, json, tag =>
        {
            found.Add(tag);
            return tag;
        });
        return found;
    }

    /// <summary>The file with <paramref name="from"/> and every tag below it renamed to start with <paramref name="to"/>.</summary>
    public byte[]? Rename(string path, byte[] json, string from, string to)
    {
        Tag parent = new(from);
        return Walk(path, json, tag =>
        {
            Tag held = new(tag);
            if (!held.MatchesTag(parent))
            {
                return tag;
            }

            return to + tag[from.Length..];
        });
    }

    /// <summary>The file with <paramref name="name"/> and every tag below it taken out of lists, and emptied where a value holds one tag.</summary>
    public byte[]? Remove(string path, byte[] json, string name)
    {
        Tag parent = new(name);
        return Walk(path, json, tag =>
        {
            Tag held = new(tag);
            if (held.MatchesTag(parent))
            {
                return null;
            }

            return tag;
        });
    }

    /// <summary>Visits every tag of the file; <paramref name="visit"/> gives the tag's new name, the same to keep it, or null to remove it.</summary>
    private byte[]? Walk(string path, byte[] json, Func<string, string?> visit)
    {
        if (Parse(json) is not JsonObject root)
        {
            return null;
        }

        bool changed;
        try
        {
            changed = WalkFile(path, root, visit);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }

        if (!changed)
        {
            return null;
        }

        string text = root.ToJsonString(WriteOptions);
        return Encoding.UTF8.GetBytes(text + Environment.NewLine);
    }

    private static JsonNode? Parse(byte[] json)
    {
        try
        {
            return JsonNode.Parse(json, documentOptions: ParseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool WalkFile(string path, JsonObject root, Func<string, string?> visit)
    {
        string extension = Path.GetExtension(path);
        switch (extension)
        {
            case SceneData.Extension:
                return WalkScene(root, visit);
            case EntityTemplates.Extension:
                return WalkComponents(root["components"] as JsonObject, visit);
            case DataAssets.TableExtension:
                return WalkTable(path, root, visit);
            default:
                if (extension == DataAssets.AssetExtension || game.Data.IsAssetFile(path))
                {
                    object asset = game.Data.ReadAsset(path, (JsonObject)root.DeepClone());
                    JsonTypeInfo typeInfo = game.Data.Options.GetTypeInfo(asset.GetType());
                    return WalkValue(typeInfo, root, visit);
                }

                if (game.Schemas.Files.TryGetValue(extension, out Func<JsonTypeInfo>? file))
                {
                    JsonTypeInfo typeInfo = file();
                    return WalkValue(typeInfo, root, visit);
                }

                return false;
        }
    }

    private bool WalkScene(JsonObject root, Func<string, string?> visit)
    {
        if (root["entities"] is not JsonArray entities)
        {
            return false;
        }

        bool changed = false;
        foreach (JsonNode? entity in entities)
        {
            changed |= WalkComponents(entity?["components"] as JsonObject, visit);
        }

        return changed;
    }

    private bool WalkComponents(JsonObject? components, Func<string, string?> visit)
    {
        if (components is null)
        {
            return false;
        }

        bool changed = false;
        foreach ((string name, JsonNode? value) in components.ToList())
        {
            SceneComponent? component = game.Spawner.Components.FirstOrDefault(registered => registered.Name == name);
            if (component is not null && value is not null)
            {
                changed |= WalkValue(component.TypeInfo, value, visit);
            }
        }

        return changed;
    }

    private bool WalkTable(string path, JsonObject root, Func<string, string?> visit)
    {
        game.Data.ReadRows(path, (JsonObject)root.DeepClone(), out Type type);
        if (root["rows"] is not JsonObject rows)
        {
            return false;
        }

        JsonTypeInfo typeInfo = game.Data.Options.GetTypeInfo(type);
        bool changed = false;
        foreach ((string _, JsonNode? row) in rows.ToList())
        {
            if (row is not null)
            {
                changed |= WalkValue(typeInfo, row, visit);
            }
        }

        return changed;
    }

    private static bool WalkValue(JsonTypeInfo typeInfo, JsonNode node, Func<string, string?> visit)
    {
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object when node is JsonObject value:
                return WalkProperties(typeInfo, value, visit);
            case JsonTypeInfoKind.Enumerable when node is JsonArray items:
                return WalkItems(typeInfo, items, visit);
            case JsonTypeInfoKind.Dictionary when node is JsonObject entries:
                return WalkEntries(typeInfo, entries, visit);
            default:
                return false;
        }
    }

    private static bool WalkProperties(JsonTypeInfo typeInfo, JsonObject value, Func<string, string?> visit)
    {
        JsonTypeInfo shape = GetShape(typeInfo, value);
        bool changed = false;
        foreach (JsonPropertyInfo property in shape.Properties)
        {
            if (value[property.Name] is not { } member)
            {
                continue;
            }

            if (IsTag(property.PropertyType))
            {
                changed |= VisitOne(value, property.Name, member, visit);
            }
            else if (FindTypeInfo(shape.Options, property.PropertyType) is { } memberType)
            {
                changed |= WalkValue(memberType, member, visit);
            }
        }

        return changed;
    }

    private static bool WalkItems(JsonTypeInfo typeInfo, JsonArray items, Func<string, string?> visit)
    {
        if (IsTag(typeInfo.ElementType))
        {
            bool changed = false;
            int index = 0;
            while (index < items.Count)
            {
                string? tag = Read(items[index]);
                string? next = tag is null ? tag : visit(tag);
                if (tag is not null && next is null)
                {
                    items.RemoveAt(index);
                    changed = true;
                    continue;
                }

                if (next != tag)
                {
                    items[index] = next;
                    changed = true;
                }

                index++;
            }

            return changed;
        }

        if (FindTypeInfo(typeInfo.Options, typeInfo.ElementType) is not { } item)
        {
            return false;
        }

        bool walked = false;
        foreach (JsonNode? element in items)
        {
            if (element is not null)
            {
                walked |= WalkValue(item, element, visit);
            }
        }

        return walked;
    }

    private static bool WalkEntries(JsonTypeInfo typeInfo, JsonObject entries, Func<string, string?> visit)
    {
        bool tags = IsTag(typeInfo.ElementType);
        JsonTypeInfo? entry = tags ? null : FindTypeInfo(typeInfo.Options, typeInfo.ElementType);
        bool changed = false;
        foreach ((string key, JsonNode? value) in entries.ToList())
        {
            if (value is null)
            {
                continue;
            }

            if (tags)
            {
                changed |= VisitOne(entries, key, value, visit);
            }
            else if (entry is not null)
            {
                changed |= WalkValue(entry, value, visit);
            }
        }

        return changed;
    }

    /// <summary>Visits a value holding one tag, which a removal empties.</summary>
    private static bool VisitOne(JsonObject owner, string name, JsonNode member, Func<string, string?> visit)
    {
        if (Read(member) is not { } tag)
        {
            return false;
        }

        string next = visit(tag) ?? string.Empty;
        if (next == tag)
        {
            return false;
        }

        owner[name] = next;
        return true;
    }

    private static string? Read(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue(out string? tag) && tag.Length > 0)
        {
            return tag;
        }

        return null;
    }

    private static bool IsTag(Type? type)
    {
        if (type is null)
        {
            return false;
        }

        Type held = Nullable.GetUnderlyingType(type) ?? type;
        return held == typeof(Tag);
    }

    /// <summary>The type info of the derived type the value names with its type discriminator, or else the type's own.</summary>
    private static JsonTypeInfo GetShape(JsonTypeInfo typeInfo, JsonObject value)
    {
        if (typeInfo.PolymorphismOptions is not { } polymorphism)
        {
            return typeInfo;
        }

        if (Read(value[polymorphism.TypeDiscriminatorPropertyName]) is not { } name)
        {
            return typeInfo;
        }

        foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
        {
            if (derived.TypeDiscriminator is string named && named == name && FindTypeInfo(typeInfo.Options, derived.DerivedType) is { } shape)
            {
                return shape;
            }
        }

        return typeInfo;
    }

    private static JsonTypeInfo? FindTypeInfo(JsonSerializerOptions options, Type? type)
    {
        if (type is null)
        {
            return null;
        }

        Type held = Nullable.GetUnderlyingType(type) ?? type;
        options.TryGetTypeInfo(held, out JsonTypeInfo? typeInfo);
        return typeInfo;
    }
}
