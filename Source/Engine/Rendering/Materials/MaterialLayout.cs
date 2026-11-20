using System.Runtime.InteropServices;
using System.Text.Json;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Packs a material's values into the bytes its shader reads: each declared parameter in order, as tightly packed floats,
/// matching an HLSL struct of the same fields loaded from a ByteAddressBuffer.
/// </summary>
public static class MaterialLayout
{
    public static byte[] Pack(IReadOnlyList<ShaderParameter> parameters, MaterialFile? material)
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
