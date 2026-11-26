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
    private string _engineShaders = MasterImporter.EngineShaders;
    private string? _engineCopy;

    /// <summary>Builds against a copy of the engine's shaders, which the test may change.</summary>
    public MasterImporterTestsBuilder WithEngineCopy()
    {
        _engineCopy = Path.Combine(Path.GetTempPath(), "FlickerVoidEngineShaders", Guid.NewGuid().ToString("N"));
        foreach (string file in Directory.EnumerateFiles(MasterImporter.EngineShaders, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(MasterImporter.EngineShaders, file);
            string target = Path.Combine(_engineCopy, relative);
            string folder = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(folder);
            File.Copy(file, target);
        }

        _engineShaders = _engineCopy;
        return this;
    }

    public void AppendEngineShader(string name, string text)
    {
        string path = Path.Combine(_engineShaders, name);
        File.AppendAllText(path, text);
    }

    public MasterImporterTestsBuilder WithSource(string path, string text)
    {
        _files.WithSource(path, text);
        return this;
    }

    public AssetBuilder Build()
    {
        MasterImporter masters = new(_compiler, _engineShaders);
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
        if (_engineCopy is not null)
        {
            Directory.Delete(_engineCopy, true);
        }
    }
}
