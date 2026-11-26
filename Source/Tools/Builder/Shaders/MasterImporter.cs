using System.Reflection;
using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Tools.Builder.Caching;
using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Tools.ShaderCompiler;

namespace FlickerVoid.Tools.Builder.Shaders;

/// <summary>
/// Builds <c>Name.fvmaster</c> master materials: copies the file, which the renderer reads for the master's parameters, and
/// compiles the geometry pass of <c>Name.hlsl</c>'s <c>SurfaceOf</c> (<see cref="MasterCode"/>) into
/// <c>Name.GeometryMain.spv</c>, its includes found beside it and then among the engine's rendering shaders.
/// </summary>
public sealed class MasterImporter(IShaderCompiler compiler, string engineShaders) : IAssetImporter
{
    public string Name => "Master";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [MasterFile.Extension];

    /// <summary>The engine's rendering shaders the builder was built beside, which masters include.</summary>
    public static string EngineShaders
    {
        get
        {
            IEnumerable<AssemblyMetadataAttribute> metadata = typeof(MasterImporter).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            AssemblyMetadataAttribute shaders = metadata.First(static attribute => attribute.Key == "EngineShaders");
            return Path.GetFullPath(shaders.Value!);
        }
    }

    public void Import(AssetImportContext context)
    {
        byte[] json = File.ReadAllBytes(context.SourceFullPath);
        MasterFile master = JsonSerializer.Deserialize(json, RenderingJsonContext.Default.MasterFile)
            ?? throw new InvalidDataException($"{context.SourcePath} is empty.");
        string name = context.SourcePath[..^MasterFile.Extension.Length];
        string hlslPath = context.SourceFullPath[..^MasterFile.Extension.Length] + ".hlsl";
        if (!File.Exists(hlslPath))
        {
            throw new InvalidDataException($"{context.SourcePath} has no {Path.GetFileName(hlslPath)} beside it.");
        }

        string[] includeDirectories = [engineShaders];
        ShaderIncludes.Record(context, hlslPath, includeDirectories);
        int offset = GetSceneParametersSize();
        string hlslFile = Path.GetFileName(hlslPath);
        string code = MasterCode.Generate(hlslFile, master.Parameters, offset);
        // Named beside the master's HLSL, so its quoted includes are found there first.
        string generatedPath = context.SourceFullPath + ".hlsl";
        ShaderSource source = new(code, MasterFile.GeometryEntryPoint, ShaderStage.Pixel) { Name = generatedPath, IncludeDirectories = includeDirectories };
        ShaderCompileResult result = compiler.Compile(source);
        if (!result.Succeeded)
        {
            throw new InvalidDataException($"The master's geometry pass failed to compile:\n{result.Messages}");
        }

        string shaderPath = context.GetOutputPath($"{name}.{MasterFile.GeometryEntryPoint}.spv");
        File.WriteAllBytes(shaderPath, result.Bytecode);
        string copyPath = context.GetOutputPath(context.SourcePath);
        File.WriteAllBytes(copyPath, json);
    }

    /// <summary>Where a master's parameters start in a material's buffer: after the scene shader's.</summary>
    private int GetSceneParametersSize()
    {
        string scenePath = Path.Combine(engineShaders, "Scene.fvshader");
        string json = File.ReadAllText(scenePath);
        ShaderAssetData scene = JsonSerializer.Deserialize(json, BuilderJsonContext.Default.ShaderAssetData)
            ?? throw new InvalidDataException($"{scenePath} is empty.");
        List<ShaderParameter> parameters = scene.Parameters ?? [];
        return MaterialLayout.Size(parameters);
    }
}
