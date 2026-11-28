using System.Collections;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Core.Data;

/// <summary>
/// The form of one object in a data asset, generated from its source-generated metadata and edited in place: a field per value,
/// a section per nested object and a list per collection of objects, such as fragments; <paramref name="edited"/> follows each edit.
/// Properties named in <paramref name="hidden"/> (by their JSON names) get no field, such as what another editor sets.
/// </summary>
public sealed class DataObjectViewModel
{
    public DataObjectViewModel(string title, object target, JsonTypeInfo typeInfo, Action edited, FindContentFiles? files = null, IReadOnlyCollection<string>? hidden = null)
    {
        Title = title;
        Target = target;
        List<FieldViewModel> fields = [];
        List<DataObjectViewModel> objects = [];
        List<DataListViewModel> lists = [];
        foreach (JsonPropertyInfo info in typeInfo.Properties)
        {
            if (info.Get is null || hidden?.Contains(info.Name) == true)
            {
                continue;
            }

            string label = DisplayNames.FromIdentifier(info.Name);
            JsonTypeInfo propertyType = typeInfo.Options.GetTypeInfo(info.PropertyType);
            object? value = info.Get(target);
            if (IsList(propertyType, value, out JsonTypeInfo? elementType))
            {
                DataListViewModel list = new(label, (IList)value!, elementType!, edited, files);
                lists.Add(list);
                continue;
            }

            if (propertyType.Kind == JsonTypeInfoKind.Object && !info.PropertyType.IsValueType && value is not null)
            {
                JsonTypeInfo valueType = typeInfo.Options.GetTypeInfo(value.GetType());
                DataObjectViewModel section = new(label, value, valueType, edited, files);
                objects.Add(section);
                continue;
            }

            ObjectProperty property = new(target, info, edited);
            FieldViewModel field = FieldFactory.Create(property, files);
            fields.Add(field);
        }

        Fields = fields;
        Objects = objects;
        Lists = lists;
    }

    public string Title { get; }

    /// <summary>The name of the icon its card shows before the title; none for a plain card.</summary>
    public string? Icon { get; init; }

    public object Target { get; }

    public IReadOnlyList<FieldViewModel> Fields { get; }

    public IReadOnlyList<DataObjectViewModel> Objects { get; }

    public IReadOnlyList<DataListViewModel> Lists { get; }

    /// <summary>Whether the value is a list of objects, which gets a section per item rather than one field.</summary>
    private static bool IsList(JsonTypeInfo typeInfo, object? value, out JsonTypeInfo? elementType)
    {
        elementType = null;
        if (typeInfo.Kind != JsonTypeInfoKind.Enumerable || typeInfo.ElementType is not { } element || value is not IList)
        {
            return false;
        }

        JsonTypeInfo elementInfo = typeInfo.Options.GetTypeInfo(element);
        if (elementInfo.Kind != JsonTypeInfoKind.Object || element.IsValueType)
        {
            return false;
        }

        elementType = elementInfo;
        return true;
    }
}
