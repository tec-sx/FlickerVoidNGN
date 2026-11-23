using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// A list or array of anything but plain strings, such as a shot's keys or an area's tags: a field per item, added at the end and
/// removed one by one, each change writing a new list.
/// </summary>
public sealed partial class CollectionFieldViewModel(IEditableValue property, FindContentFiles? files) : FieldViewModel(property)
{
    private readonly JsonTypeInfo _elementType = GetElementType(property);

    public ObservableCollection<FieldViewModel> Items { get; } = [];

    public override bool IsNested => true;

    /// <summary>Adds an item at its type's defaults; a polymorphic item starts as the first kind, switched in its own field.</summary>
    [RelayCommand]
    public void Add()
    {
        if (IsReadOnly)
        {
            return;
        }

        List<object?> items = EditableJson.ReadItems(Property);
        object item = CreateItem();
        items.Add(item);
        EditableJson.WriteItems(Property, items);
    }

    [RelayCommand]
    public void Remove(FieldViewModel? item)
    {
        int index = item is null ? -1 : Items.IndexOf(item);
        if (index < 0 || IsReadOnly)
        {
            return;
        }

        List<object?> items = EditableJson.ReadItems(Property);
        items.RemoveAt(index);
        EditableJson.WriteItems(Property, items);
    }

    /// <summary>Makes a field per item when the count changes, else shows each item's value.</summary>
    protected override void Show(object? value)
    {
        int count = value is ICollection items ? items.Count : 0;
        if (count != Items.Count)
        {
            Items.Clear();
            for (int index = 0; index < count; index++)
            {
                ElementValue element = new(Property, _elementType.Type, index);
                FieldViewModel field = FieldFactory.Create(element, files);
                Items.Add(field);
            }

            return;
        }

        foreach (FieldViewModel field in Items)
        {
            field.Refresh();
        }
    }

    private static JsonTypeInfo GetElementType(IEditableValue property)
    {
        JsonTypeInfo listType = property.Options!.GetTypeInfo(property.Type);
        return property.Options.GetTypeInfo(listType.ElementType!);
    }

    private object CreateItem()
    {
        Type type = _elementType.Type;
        if (type == typeof(string))
        {
            return "";
        }

        JsonTypeInfo typeInfo = _elementType;
        if (_elementType.PolymorphismOptions is { DerivedTypes.Count: > 0 } polymorphism)
        {
            Type first = polymorphism.DerivedTypes[0].DerivedType;
            typeInfo = _elementType.Options.GetTypeInfo(first);
        }

        if (typeInfo.CreateObject is { } create)
        {
            return create();
        }

        return RuntimeHelpers.GetUninitializedObject(type);
    }
}
