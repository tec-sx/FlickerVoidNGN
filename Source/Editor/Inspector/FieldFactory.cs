using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>Picks the field for a property from its type and the attributes the component gives it.</summary>
public static class FieldFactory
{
    public static FieldViewModel Create(ComponentProperty property)
    {
        FieldViewModel field = CreateEmpty(property);
        field.Refresh();
        return field;
    }

    private static FieldViewModel CreateEmpty(ComponentProperty property)
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
            return new TextFieldViewModel(property);
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(int))
        {
            return new NumberFieldViewModel(property);
        }

        if (type.IsEnum)
        {
            return new EnumFieldViewModel(property);
        }

        return new ReadOnlyFieldViewModel(property);
    }
}
