using System.Runtime.InteropServices;
using System.Text.Json;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Packs a material's values into the bytes its shader reads: each declared parameter in order, as tightly packed floats,
/// and textures as the uint indices of their image and relief (<see cref="MaterialTexture"/>, <see cref="NoTexture"/> when unset), matching an HLSL struct of the same
/// fields loaded from a ByteAddressBuffer.
/// </summary>
public static class MaterialLayout
{
    /// <summary>The index of an unset texture, the shaders' <c>None</c>.</summary>
    public const uint NoTexture = uint.MaxValue;

    /// <summary>How many bytes the parameters take packed: where a master's own parameters start after the scene shader's.</summary>
    public static int Size(IReadOnlyList<ShaderParameter> parameters)
    {
        return parameters.Sum(static parameter => parameter.Components) * sizeof(float);
    }

    /// <summary>Packs the values; <paramref name="loadTexture"/> loads a texture by its content path and gives its indices.</summary>
    public static byte[] Pack(IReadOnlyList<ShaderParameter> parameters, MaterialFile? material, Func<string, MaterialTexture>? loadTexture = null)
    {
        if (material is not null)
        {
            CheckNames(parameters, material);
        }

        List<float> values = [];
        foreach (ShaderParameter parameter in parameters)
        {
            JsonElement value = parameter.Default;
            if (material is not null && material.Parameters.TryGetValue(parameter.Name, out JsonElement set))
            {
                value = set;
            }

            if (parameter.Type == ShaderParameterType.Texture)
            {
                MaterialTexture texture = LoadTexture(parameter, value, loadTexture);
                values.Add(BitConverter.UInt32BitsToSingle(texture.Image));
                values.Add(BitConverter.UInt32BitsToSingle(texture.Relief));
                continue;
            }

            AddValue(values, parameter, value);
        }

        Span<float> floats = CollectionsMarshal.AsSpan(values);
        return MemoryMarshal.AsBytes(floats).ToArray();
    }

    private static void CheckNames(IReadOnlyList<ShaderParameter> parameters, MaterialFile material)
    {
        foreach (string name in material.Parameters.Keys)
        {
            bool declared = parameters.Any(parameter => parameter.Name == name);
            if (!declared)
            {
                throw new InvalidDataException($"The shader has no material parameter named '{name}'.");
            }
        }
    }

    private static MaterialTexture LoadTexture(ShaderParameter parameter, JsonElement value, Func<string, MaterialTexture>? loadTexture)
    {
        MaterialTexture none = new(NoTexture, NoTexture);
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return none;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The material parameter '{parameter.Name}' needs a texture's path.");
        }

        string? path = value.GetString();
        if (string.IsNullOrEmpty(path) || loadTexture is null)
        {
            return none;
        }

        return loadTexture(path);
    }

    private static void AddValue(List<float> values, ShaderParameter parameter, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && parameter.Components == 1)
        {
            values.Add(value.GetSingle());
            return;
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != parameter.Components)
        {
            throw new InvalidDataException($"The material parameter '{parameter.Name}' needs {parameter.Components} number(s).");
        }

        foreach (JsonElement component in value.EnumerateArray())
        {
            values.Add(component.GetSingle());
        }
    }
}
