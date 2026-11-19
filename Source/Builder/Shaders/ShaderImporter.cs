using System.Text.Json;
using System.Text.RegularExpressions;

using FlickerVoid.Engine.Builder.Caching;
using FlickerVoid.Engine.Builder.Importing;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Builder.Shaders;

/// <summary>
/// Compiles <c>Name.shader.json</c> assets: each listed entry point of <c>Name.hlsl</c> becomes <c>Name.EntryPoint.spv</c>.
/// The HLSL and the files it includes are recorded as dependencies.
/// </summary>
public sealed partial class ShaderImporter(IShaderCompiler compiler) : IAssetImporter
{
    private const string Extension = ".shader.json";

    public string Name => "Shader";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [Extension];

    public void Import(AssetImportContext context)
    {
        string json = File.ReadAllText(context.SourceFullPath);
        ShaderAssetData asset = JsonSerializer.Deserialize(json, BuilderJsonContext.Default.ShaderAssetData)
            ?? throw new InvalidDataException($"{context.SourcePath} is empty.");
        string name = context.SourcePath[..^Extension.Length];
        string hlslPath = context.SourceFullPath[..^Extension.Length] + ".hlsl";
        AddIncludes(context, hlslPath, []);
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
    }

    /// <summary>Records the file and, through its quoted includes, every file it reads; a missing one is left to the compiler.</summary>
    private static void AddIncludes(AssetImportContext context, string fullPath, HashSet<string> visited)
    {
        if (!File.Exists(fullPath) || !visited.Add(fullPath))
        {
            return;
        }

        context.AddDependency(fullPath);
        string folder = Path.GetDirectoryName(fullPath)!;
        string code = File.ReadAllText(fullPath);
        foreach (Match include in IncludePattern().Matches(code))
        {
            string includedPath = Path.GetFullPath(include.Groups[1].Value, folder);
            AddIncludes(context, includedPath, visited);
        }
    }

    [GeneratedRegex("""^\s*#\s*include\s*"([^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex IncludePattern();
}
