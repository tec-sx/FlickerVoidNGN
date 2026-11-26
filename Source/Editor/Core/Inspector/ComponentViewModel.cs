using System.Text.Json.Serialization.Metadata;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>One component of the selected entity, with a field for each property its scene file metadata lists.</summary>
public sealed partial class ComponentViewModel : ObservableObject
{
    /// <summary>How many of its values a folded component says on its one line.</summary>
    private const int SummaryParts = 2;

    public ComponentViewModel(EditorScene scene, Entity entity, SceneComponent component, FindContentFiles files)
    {
        Title = DisplayNames.FromIdentifier(component.TypeInfo.Type.Name);
        Category = ComponentChoices.GetModule(component.TypeInfo.Type);
        Icon = ComponentChoices.GetIcon(component.Name, Category);
        List<FieldViewModel> fields = [];
        foreach (JsonPropertyInfo info in component.TypeInfo.Properties)
        {
            if (info.Get is null)
            {
                continue;
            }

            ComponentProperty property = new(scene, entity, component, info);
            FieldViewModel field = FieldFactory.Create(property, files);
            fields.Add(field);
        }

        Fields = fields;
        Component = component;
    }

    public SceneComponent Component { get; }

    /// <summary>The transform stays: the viewport, picking and the gizmo need it.</summary>
    public bool CanRemove => Component.Name != "transform";

    public string Title { get; }

    /// <summary>The module it comes from (Rendering, Physics, Gameplay), its place on the inspector's category rail.</summary>
    public string Category { get; }

    /// <summary>The name of its icon on its header.</summary>
    public string Icon { get; }

    public IReadOnlyList<FieldViewModel> Fields { get; }

    /// <summary>Whether its fields show; folded, its header says <see cref="Summary"/> instead.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>What a folded component holds, on one line: its first values that say something (sight 8, hearing 12).</summary>
    public string Summary
    {
        get
        {
            List<string> parts = [];
            foreach (FieldViewModel shown in Fields)
            {
                string? part = Describe(shown);
                if (part is not null)
                {
                    parts.Add(part);
                }

                if (parts.Count == SummaryParts)
                {
                    break;
                }
            }

            return string.Join(", ", parts);
        }
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(Summary));

    /// <summary>A field in a few words, or null when it has nothing short to say (vectors, lists, switched off, empty).</summary>
    private static string? Describe(FieldViewModel field)
    {
        string label = field.Label.ToLowerInvariant();
        switch (field)
        {
            case NumberFieldViewModel number:
                string value = number.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                return $"{label} {value}";
            case BoolFieldViewModel { Value: true }:
                return label;
            case TextFieldViewModel { Value.Length: > 0 } text:
                return text.Value;
            case EnumFieldViewModel { Value: { } choice }:
                return choice.ToString();
            case AssetFieldViewModel { Picker.Current: { } path }:
                return Path.GetFileNameWithoutExtension(path);
            default:
                return null;
        }
    }

    public void Refresh()
    {
        foreach (FieldViewModel field in Fields)
        {
            field.Refresh();
        }
    }
}
