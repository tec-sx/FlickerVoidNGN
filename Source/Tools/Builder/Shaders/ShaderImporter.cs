using System.Text.Json;

using FlickerVoid.Tools.Builder.Caching;
using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Tools.ShaderCompiler;

namespace FlickerVoid.Tools.Builder.Shaders;

/// <summary>
/// Compiles <c>Name.fvshader</c> assets: each listed entry point of <c>Name.hlsl</c> becomes <c>Name.EntryPoint.spv</c>.
/// The HLSL and the files it includes are recorded as dependencies. Declared material parameters are written to
/// <c>Name.fvparams</c>, where the renderer and the editor read them.
/// </summary>
public sealed class ShaderImporter(IShaderCompiler compiler) : IAssetImporter
{
    private const string Extension = ".fvshader";

    public string Name => "Shader";

    public int Version => 3;

    public IReadOnlyList<string> Extensions { get; } = [Extension];

    public void Import(AssetImportContext context)
    {
        string json = File.ReadAllText(context.SourceFullPath);
        ShaderAssetData asset = JsonSerializer.Deserialize(json, BuilderJsonContext.Default.ShaderAssetData)
            ?? throw new InvalidDataException($"{context.SourcePath} is empty.");
        string name = context.SourcePath[..^Extension.Length];
        string hlslPath = context.SourceFullPath[..^Extension.Length] + ".hlsl";
        ShaderIncludes.Record(context, hlslPath);
        string code = File.ReadAllText(hlslPath);

        foreach ((string entryPoint, ShaderStage stage) in asset.EntryPoints)
        {
            ShaderSource source = new(code, entryPoint, stage) { Name = hlslPath };
            ShaderCompileResult result = compiler.Compile(source);
            if (!result.Succeeded)
            {
                throw new InvalidDataException($"{entryPoint} failed to compile:\n{result.Messages}");
            }

            string outputPath = context.GetOutputPath($"{name}.{entryPoint}.spv");
            File.WriteAllBytes(outputPath, result.Bytecode);
        }

        if (asset.Parameters is { Count: > 0 } parameters)
        {
            ShaderParameters declared = new() { Parameters = parameters };
            string parametersPath = context.GetOutputPath($"{name}.fvparams");
            byte[] parametersJson = JsonSerializer.SerializeToUtf8Bytes(declared, RenderingJsonContext.Default.ShaderParameters);
            File.WriteAllBytes(parametersPath, parametersJson);
        }
    }
}
