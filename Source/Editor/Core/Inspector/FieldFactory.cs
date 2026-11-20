using System.ComponentModel.DataAnnotations;
using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>Picks the field for a value from its type and the attributes that refine it.</summary>
public static class FieldFactory
{
    /// <summary>The field for the value; with <paramref name="files"/>, a field naming a content file suggests those it may name.</summary>
    public static FieldViewModel Create(IEditableValue property, FindContentFiles? files = null)
    {
        FieldViewModel field = CreateEmpty(property, files);
        field.Refresh();
        return field;
    }

    private static FieldViewModel CreateEmpty(IEditableValue property, FindContentFiles? files)
    {
        Type type = property.Type;
        if (type == typeof(Vector3))
        {
            ColorAttribute? color = property.GetAttribute<ColorAttribute>();
            if (color is not null)
            {
                return new ColorFieldViewModel(property);
            }

            return new VectorFieldViewModel(property);
        }

        if (type == typeof(Quaternion))
        {
            return new RotationFieldViewModel(property);
        }

        if (type == typeof(bool))
        {
            return new BoolFieldViewModel(property);
        }

        if (type == typeof(string))
        {
            IReadOnlyList<string> suggestions = FindSuggestions(property, files);
            return new TextFieldViewModel(property, suggestions);
        }

        if (type == typeof(string[]))
        {
            IReadOnlyList<string> suggestions = FindSuggestions(property, files);
            return new ListFieldViewModel(property, suggestions);
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(int))
        {
            RangeAttribute? range = property.GetAttribute<RangeAttribute>();
            if (range is not null)
            {
                return new RangedNumberFieldViewModel(property);
            }

            return new NumberFieldViewModel(property);
        }

        if (type.IsEnum)
        {
            return new EnumFieldViewModel(property);
        }

        return new ReadOnlyFieldViewModel(property);
    }

    private static IReadOnlyList<string> FindSuggestions(IEditableValue property, FindContentFiles? files)
    {
        ContentFileAttribute? file = property.GetAttribute<ContentFileAttribute>();
        if (file is null || files is null)
        {
            return [];
        }

        return files(file.Extensions);
    }
}
