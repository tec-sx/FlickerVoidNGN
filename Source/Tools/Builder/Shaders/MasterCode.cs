using System.Globalization;
using System.Text;

using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Tools.Builder.Shaders;

/// <summary>
/// Writes the HLSL the content build compiles for a master: the scene shader's surface code, the master's parameters as a
/// struct loaded from the material's buffer after the scene shader's, the master's own <c>SurfaceOf</c>, and the geometry pass
/// (or for glass the transparent pass) reading the surface through it.
/// </summary>
public static class MasterCode
{
    public const string GeometryFile = "Geometry.hlsl";

    public const string TransparentFile = "Transparent.hlsl";

    /// <summary>
    /// The code for the master whose HLSL is <paramref name="hlslFile"/>, with its parameters starting <paramref name="offset"/>
    /// bytes in, for the pass whose pixel shader <paramref name="passFile"/> holds.
    /// </summary>
    public static string Generate(string hlslFile, IReadOnlyList<ShaderParameter> parameters, int offset, string passFile = GeometryFile)
    {
        StringBuilder code = new();
        code.AppendLine("#include \"SceneSurface.hlsl\"");
        code.AppendLine();
        code.AppendLine("struct MasterParameters");
        code.AppendLine("{");
        foreach (ShaderParameter parameter in parameters)
        {
            string field = ToField(parameter.Name);
            switch (parameter.Type)
            {
                case ShaderParameterType.Color:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    float3 {field};");
                    break;
                case ShaderParameterType.Texture:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    uint {field};");
                    code.AppendLine(CultureInfo.InvariantCulture, $"    uint {field}Relief;");
                    break;
                default:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    float {field};");
                    break;
            }
        }

        code.AppendLine("};");
        code.AppendLine();
        code.AppendLine("MasterParameters LoadMasterParameters(uint index)");
        code.AppendLine("{");
        code.AppendLine("    ByteAddressBuffer buffer = Buffers[NonUniformResourceIndex(index)];");
        code.AppendLine("    MasterParameters parameters = (MasterParameters)0;");
        int at = offset;
        foreach (ShaderParameter parameter in parameters)
        {
            string field = ToField(parameter.Name);
            switch (parameter.Type)
            {
                case ShaderParameterType.Color:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    parameters.{field} = buffer.Load<float3>({at});");
                    break;
                case ShaderParameterType.Texture:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    parameters.{field} = buffer.Load<uint>({at});");
                    code.AppendLine(CultureInfo.InvariantCulture, $"    parameters.{field}Relief = buffer.Load<uint>({at + sizeof(uint)});");
                    break;
                default:
                    code.AppendLine(CultureInfo.InvariantCulture, $"    parameters.{field} = buffer.Load<float>({at});");
                    break;
            }

            at += parameter.Components * sizeof(float);
        }

        code.AppendLine("    return parameters;");
        code.AppendLine("}");
        code.AppendLine();
        code.AppendLine(CultureInfo.InvariantCulture, $"#include \"{hlslFile}\"");
        code.AppendLine();
        code.AppendLine("Surface SurfaceAt(VertexOutput input, Draw draw, Material material)");
        code.AppendLine("{");
        code.AppendLine("    MasterParameters parameters = LoadMasterParameters(draw.Material);");
        code.AppendLine("    return SurfaceOf(input, draw, material, parameters);");
        code.AppendLine("}");
        code.AppendLine();
        code.AppendLine(CultureInfo.InvariantCulture, $"#include \"{passFile}\"");
        return code.ToString();
    }

    /// <summary>A parameter's field in HLSL: its name with the first letter raised, as the scene shader's <c>Material</c> names them.</summary>
    private static string ToField(string name)
    {
        return string.Concat(char.ToUpperInvariant(name[0]).ToString(), name[1..]);
    }
}
