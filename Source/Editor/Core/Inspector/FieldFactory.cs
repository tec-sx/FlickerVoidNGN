using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>Picks the field for a value from its type and the attributes that refine it.</summary>
public static class FieldFactory
{
    /// <summary>The field for the value; with <paramref name="files"/>, a field naming a content file picks from those it may name.</summary>
    public static FieldViewModel Create(IEditableValue property, FindContentFiles? files = null)
    {
        FieldViewModel field = CreateEmpty(property, files);
        field.Refresh();
        return field;
    }

    private static FieldViewModel CreateEmpty(IEditableValue property, FindContentFiles? files)
    {
        Type type = property.Type;
        if (type == typeof(Tag) || type == typeof(List<Tag>) || type == typeof(Tag[]))
        {
            return new TagsFieldViewModel(property);
        }

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
            Func<IReadOnlyList<string>>? find = GetFinder(property, files);
            if (find is not null)
            {
                return new AssetFieldViewModel(property, find);
            }

            return new TextFieldViewModel(property);
        }

        if (type == typeof(string[]))
        {
            Func<IReadOnlyList<string>>? find = GetFinder(property, files);
            return new ListFieldViewModel(property, find);
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

        if (property.Options is { } options)
        {
            JsonTypeInfo typeInfo = options.GetTypeInfo(type);
            if (typeInfo.Kind == JsonTypeInfoKind.Enumerable && typeInfo.ElementType is not null)
            {
                return new CollectionFieldViewModel(property, files);
            }

            if (typeInfo.Kind == JsonTypeInfoKind.Object)
            {
                return new ObjectFieldViewModel(property, files);
            }
        }

        return new ReadOnlyFieldViewModel(property);
    }

    /// <summary>Lists the content files a value may name, when it names one and there are files to list.</summary>
    private static Func<IReadOnlyList<string>>? GetFinder(IEditableValue property, FindContentFiles? files)
    {
        ContentFileAttribute? file = property.GetAttribute<ContentFileAttribute>();
        if (file is null || files is null)
        {
            return null;
        }

        return () => files(file.Extensions);
    }
}
