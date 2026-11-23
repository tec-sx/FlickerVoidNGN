using System.Collections.ObjectModel;
using System.Text.Json.Serialization.Metadata;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>
/// A nested object, such as a camera cue's transition or a body's collider: a field per property, and for a polymorphic value the
/// kinds it may be (switching keeps the properties the kinds share).
/// </summary>
public sealed partial class ObjectFieldViewModel : FieldViewModel
{
    private const string None = "None";

    private readonly FindContentFiles? _files;
    private readonly Dictionary<string, Type> _kinds = [];
    private Type? _shownType;

    public ObjectFieldViewModel(IEditableValue property, FindContentFiles? files)
        : base(property)
    {
        _files = files;
        JsonTypeInfo typeInfo = property.Options!.GetTypeInfo(property.Type);
        if (typeInfo.PolymorphismOptions is { } polymorphism)
        {
            foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
            {
                string name = derived.TypeDiscriminator?.ToString() ?? derived.DerivedType.Name;
                string shown = DisplayNames.FromIdentifier(name);
                _kinds.Add(shown, derived.DerivedType);
            }
        }

        List<string> kinds = [.. _kinds.Keys];
        if (kinds.Count > 0 && property.AcceptsNull)
        {
            kinds.Insert(0, None);
        }

        Kinds = kinds;
    }

    public ObservableCollection<FieldViewModel> Fields { get; } = [];

    /// <summary>The kinds a polymorphic value may be, by their names in files; empty for any other object.</summary>
    public IReadOnlyList<string> Kinds { get; }

    public bool HasKinds => Kinds.Count > 0;

    [ObservableProperty]
    public partial string? Kind { get; set; }

    public override bool IsNested => true;

    protected override void Show(object? value)
    {
        Type? type = value?.GetType();
        if (type != _shownType)
        {
            _shownType = type;
            Rebuild(type);
        }
        else
        {
            foreach (FieldViewModel field in Fields)
            {
                field.Refresh();
            }
        }

        if (HasKinds)
        {
            Kind = FindKind(type);
        }
    }

    partial void OnKindChanged(string? value)
    {
        if (value is null || value == FindKind(_shownType))
        {
            return;
        }

        if (value == None)
        {
            Write(null);
            return;
        }

        object? current = Property.Get();
        Type type = _kinds[value];
        JsonTypeInfo typeInfo = Property.Options!.GetTypeInfo(type);
        object next = typeInfo.CreateObject!();
        if (current is not null)
        {
            CopyShared(current, next, typeInfo);
        }

        Write(next);
    }

    private void Rebuild(Type? type)
    {
        Fields.Clear();
        if (type is null)
        {
            return;
        }

        JsonTypeInfo typeInfo = Property.Options!.GetTypeInfo(type);
        foreach (JsonPropertyInfo info in typeInfo.Properties)
        {
            if (info.Get is null)
            {
                continue;
            }

            MemberValue member = new(Property, info);
            FieldViewModel field = FieldFactory.Create(member, _files);
            Fields.Add(field);
        }
    }

    private string? FindKind(Type? type)
    {
        if (type is null)
        {
            return HasKinds ? None : null;
        }

        return _kinds.FirstOrDefault(pair => pair.Value == type).Key;
    }

    /// <summary>Sets on the new kind each property the old one has by the same name and type, such as a collider's offset.</summary>
    private void CopyShared(object current, object next, JsonTypeInfo nextType)
    {
        Type currentType = current.GetType();
        JsonTypeInfo currentInfo = Property.Options!.GetTypeInfo(currentType);
        foreach (JsonPropertyInfo target in nextType.Properties)
        {
            JsonPropertyInfo? source = currentInfo.Properties.FirstOrDefault(candidate => candidate.Name == target.Name);
            if (source?.Get is null || target.Set is null || source.PropertyType != target.PropertyType)
            {
                continue;
            }

            object? value = source.Get(current);
            target.Set(next, value);
        }
    }
}
