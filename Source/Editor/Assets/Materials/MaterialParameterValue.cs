using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>One parameter the shader declares, read from and written to the material being edited.</summary>
public sealed class MaterialParameterValue(MaterialDocument document, ShaderParameter parameter) : IEditableValue
{
    /// <summary>The source files the content build turns into textures: images, and Rebelle paintings.</summary>
    private static readonly ContentFileAttribute TextureFiles = new(".png", ".jpg", ".jpeg", ".tga", ".bmp", ".reb");

    public string Label { get; } = DisplayNames.FromIdentifier(parameter.Name);

    public Type Type
    {
        get
        {
            if (parameter.Type == ShaderParameterType.Color)
            {
                return typeof(Vector3);
            }

            if (parameter.Type == ShaderParameterType.Texture)
            {
                return typeof(string);
            }

            return typeof(float);
        }
    }

    public bool CanWrite => true;

    /// <summary>A texture may be unset, leaving the mesh part's own.</summary>
    public bool AcceptsNull => parameter.Type == ShaderParameterType.Texture;

    /// <summary>A range from the declared limits, a colour mark on colours, and the texture files a texture can name.</summary>
    public TAttribute? GetAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        if (typeof(TAttribute) == typeof(ContentFileAttribute) && parameter.Type == ShaderParameterType.Texture)
        {
            return TextureFiles as TAttribute;
        }

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

        if (parameter.Type == ShaderParameterType.Texture)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
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
        if (parameter.Type == ShaderParameterType.Texture)
        {
            json = "null";
            if (value is string { Length: > 0 } path)
            {
                JsonEncodedText encoded = JsonEncodedText.Encode(path);
                json = $"\"{encoded}\"";
            }
        }
        else if (value is Vector3 vector)
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

    public JsonSerializerOptions? Options => null;

    public string ToJson(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}
