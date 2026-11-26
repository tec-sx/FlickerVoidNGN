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

    /// <summary>
    /// The bytes after the scene shader's parameters that hold the material's shading model (a uint) and the four values it
    /// reads (<see cref="ShadingModels"/>), which the lighting pass loads beside the scene shader's; a master's own follow them.
    /// </summary>
    public const int ModelSize = 5 * sizeof(float);

    /// <summary>How many bytes the parameters take packed.</summary>
    public static int Size(IReadOnlyList<ShaderParameter> parameters)
    {
        return parameters.Sum(static parameter => parameter.Components) * sizeof(float);
    }

    /// <summary>Where a master's own parameters start in a material's buffer: after the scene shader's and the shading model's.</summary>
    public static int MasterOffset(IReadOnlyList<ShaderParameter> scene)
    {
        return Size(scene) + ModelSize;
    }

    /// <summary>
    /// Packs a material for the renderer: the scene shader's parameters, its master's shading model and the values the model reads,
    /// then the master's own parameters; without a master, the painted model and no more. Every name the material sets must be
    /// declared by one or the other.
    /// </summary>
    public static byte[] PackInstance(IReadOnlyList<ShaderParameter> scene, MasterFile? master, MaterialFile? material, Func<string, MaterialTexture>? loadTexture = null)
    {
        List<ShaderParameter> masterParameters = master?.Parameters ?? [];
        if (material is not null)
        {
            IReadOnlyList<ShaderParameter> all = [.. scene, .. masterParameters];
            CheckNames(all, material);
        }

        List<float> values = [];
        AddValues(values, scene, material, loadTexture);
        ShadingModel model = master?.ShadingModel ?? ShadingModel.Painted;
        values.Add(BitConverter.UInt32BitsToSingle((uint)model));
        List<float> modelValues = [];
        foreach (string name in ShadingModels.GetValues(model))
        {
            ShaderParameter? declared = masterParameters.FirstOrDefault(parameter => parameter.Name == name);
            if (declared is null || declared.Type == ShaderParameterType.Texture)
            {
                modelValues.Add(0);
                continue;
            }

            JsonElement value = declared.Default;
            if (material is not null && material.Parameters.TryGetValue(name, out JsonElement set))
            {
                value = set;
            }

            AddValue(modelValues, declared, value);
        }

        for (int index = 0; index < 4; index++)
        {
            float value = 0;
            if (index < modelValues.Count)
            {
                value = modelValues[index];
            }

            values.Add(value);
        }

        AddValues(values, masterParameters, material, loadTexture);
        Span<float> floats = CollectionsMarshal.AsSpan(values);
        return MemoryMarshal.AsBytes(floats).ToArray();
    }

    /// <summary>Packs the values; <paramref name="loadTexture"/> loads a texture by its content path and gives its indices.</summary>
    public static byte[] Pack(IReadOnlyList<ShaderParameter> parameters, MaterialFile? material, Func<string, MaterialTexture>? loadTexture = null)
    {
        if (material is not null)
        {
            CheckNames(parameters, material);
        }

        List<float> values = [];
        AddValues(values, parameters, material, loadTexture);
        Span<float> floats = CollectionsMarshal.AsSpan(values);
        return MemoryMarshal.AsBytes(floats).ToArray();
    }

    private static void AddValues(List<float> values, IReadOnlyList<ShaderParameter> parameters, MaterialFile? material, Func<string, MaterialTexture>? loadTexture)
    {
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
