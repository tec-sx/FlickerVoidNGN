using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Core.Data;

/// <summary>
/// A list of objects in a data asset, such as a definition's fragments: a form per item, items added by type (each derived type a
/// file may name with <c>$type</c>, or the element type itself) and removed; <paramref name="edited"/> follows each change.
/// </summary>
public sealed partial class DataListViewModel
{
    private readonly IList _list;
    private readonly JsonTypeInfo _elementType;
    private readonly Action _edited;
    private readonly FindContentFiles? _files;
    private readonly Dictionary<string, Type> _choices = [];

    public DataListViewModel(string label, IList list, JsonTypeInfo elementType, Action edited, FindContentFiles? files = null)
    {
        Label = label;
        _list = list;
        _elementType = elementType;
        _edited = edited;
        _files = files;
        if (elementType.PolymorphismOptions is { } polymorphism)
        {
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                string name = derived.TypeDiscriminator?.ToString() ?? derived.DerivedType.Name;
                _choices.Add(name, derived.DerivedType);
            }
        }
        else if (elementType.CreateObject is not null)
        {
            _choices.Add(DisplayNames.FromIdentifier(elementType.Type.Name), elementType.Type);
        }

        Choices = [.. _choices.Keys];
        List<DataChoice> addChoices = [];
        foreach (string choice in Choices)
        {
            RelayCommand add = new(() => Add(choice));
            addChoices.Add(new DataChoice(choice, add));
        }

        AddChoices = addChoices;
        foreach (object? item in list)
        {
            if (item is not null)
            {
                DataListItemViewModel created = CreateItem(item);
                Items.Add(created);
            }
        }
    }

    public string Label { get; }

    /// <summary>The types an added item may be, by the name files give them.</summary>
    public IReadOnlyList<string> Choices { get; }

    /// <summary>The same types, each with the command that adds an item of it, for a menu.</summary>
    public IReadOnlyList<DataChoice> AddChoices { get; }

    public ObservableCollection<DataListItemViewModel> Items { get; } = [];

    [RelayCommand]
    public void Add(string choice)
    {
        Type type = _choices[choice];
        JsonTypeInfo typeInfo = _elementType.Options.GetTypeInfo(type);
        object item = typeInfo.CreateObject!();
        _list.Add(item);
        DataListItemViewModel created = CreateItem(item);
        Items.Add(created);
        _edited();
    }

    [RelayCommand]
    public void Remove(DataListItemViewModel item)
    {
        int index = Items.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        Items.RemoveAt(index);
        _list.RemoveAt(index);
        _edited();
    }

    private DataListItemViewModel CreateItem(object item)
    {
        Type type = item.GetType();
        string title = _choices.FirstOrDefault(pair => pair.Value == type).Key ?? DisplayNames.FromIdentifier(type.Name);
        JsonTypeInfo typeInfo = _elementType.Options.GetTypeInfo(type);
        DataObjectViewModel form = new(title, item, typeInfo, _edited, _files);
        return new DataListItemViewModel(this, form);
    }
}
