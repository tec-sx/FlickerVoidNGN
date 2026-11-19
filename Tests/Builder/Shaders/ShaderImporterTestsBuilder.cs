using FlickerVoid.Engine.Builder.Importing;
using FlickerVoid.Engine.Builder.Shaders;
using FlickerVoid.Engine.Shaders;

namespace FlickerVoid.Engine.Builder.Tests.Shaders;

/// <summary>
/// Builds an asset builder with the shader importer under test on DXC, skipping HLSL sources, over new temporary source and
/// output folders that are deleted when disposed.
/// </summary>
internal sealed class ShaderImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();
    private readonly DxcShaderCompiler _compiler = new();

    public ShaderImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build()
    {
        ShaderImporter shaders = new(_compiler);
        SkipImporter sources = new(".hlsl", ".hlsli");
        return new AssetBuilder([shaders, sources]);
    }

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    public void WriteSource(string path, string text) => _files.WriteSource(path, text);

    public bool OutputExists(string path) => _files.OutputExists(path);

    public byte[] ReadOutput(string path)
    {
        string fullPath = Path.Combine(_files.OutputDirectory, path);
        return File.ReadAllBytes(fullPath);
    }

    public void Dispose()
    {
        _compiler.Dispose();
        _files.Dispose();
    }
}
