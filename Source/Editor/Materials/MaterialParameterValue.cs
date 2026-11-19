using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Materials;

/// <summary>One parameter the shader declares, read from and written to the material being edited.</summary>
public sealed class MaterialParameterValue(MaterialDocument document, ShaderParameter parameter) : IEditableValue
{
    public string Label { get; } = DisplayNames.FromIdentifier(parameter.Name);

    public Type Type
    {
        get
        {
            if (parameter.Type == ShaderParameterType.Color)
            {
                return typeof(Vector3);
            }

            return typeof(float);
        }
    }

    public bool CanWrite => true;

    public bool AcceptsNull => false;

    /// <summary>A range from the declared limits, and a colour mark on colours.</summary>
    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        if (typeof(TAttribute) == typeof(ColorAttribute) && parameter.Type == ShaderParameterType.Color)
        {
            return new ColorAttribute() as TAttribute;
        }

        if (typeof(TAttribute) == typeof(RangeAttribute) && parameter.Minimum is { } minimum && parameter.Maximum is { } maximum)
        {
            return new RangeAttribute(minimum, maximum) as TAttribute;
        }

        return null;
    }

    public object? Get()
    {
        JsonElement value = parameter.Default;
        if (document.Material.Parameters.TryGetValue(parameter.Name, out JsonElement set))
        {
            value = set;
        }

        if (parameter.Type == ShaderParameterType.Color)
        {
            float x = value[0].GetSingle();
            float y = value[1].GetSingle();
            float z = value[2].GetSingle();
            return new Vector3(x, y, z);
        }

        return value.GetSingle();
    }

    public void Set(object? value)
    {
        string json;
        if (value is Vector3 vector)
        {
            json = string.Create(CultureInfo.InvariantCulture, $"[{vector.X}, {vector.Y}, {vector.Z}]");
        }
        else
        {
            float number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            json = number.ToString(CultureInfo.InvariantCulture);
        }

        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement element = parsed.RootElement.Clone();
        document.Set(parameter.Name, element);
    }

    public string ToJson(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}
