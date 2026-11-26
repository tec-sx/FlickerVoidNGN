using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Tools.Builder.Shaders;
using FlickerVoid.Tools.ShaderCompiler;

namespace FlickerVoid.Tools.Builder.Tests.Shaders;

/// <summary>
/// Builds an asset builder with the master importer under test on DXC and the engine's rendering shaders, skipping HLSL sources,
/// over new temporary source and output folders that are deleted when disposed.
/// </summary>
internal sealed class MasterImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();
    private readonly DxcShaderCompiler _compiler = new();

    public MasterImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build()
    {
        MasterImporter masters = new(_compiler, MasterImporter.EngineShaders);
        SkipImporter sources = new(".hlsl", ".hlsli");
        return new AssetBuilder([masters, sources]);
    }

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    public void WriteSource(string path, string text) => _files.WriteSource(path, text);

    public bool OutputExists(string path) => _files.OutputExists(path);

    public string ReadOutputText(string path) => _files.ReadOutput(path);

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
