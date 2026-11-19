using FlickerVoid.Engine.Builder.Models;
using FlickerVoid.Engine.Rendering.Meshes;

using SharpGLTF.Schema2;

namespace FlickerVoid.Engine.Builder.Tests.Models;

/// <summary>
/// Builds an asset builder with the model importer under test, over new temporary source and output folders that are deleted
/// when disposed, and writes glTF models made by <see cref="GltfImporterTestsBuilder"/> into the source folder.
/// </summary>
internal sealed class ModelImporterTestsBuilder : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();

    /// <summary>Writes a binary .glb.</summary>
    public ModelImporterTestsBuilder WithGlb(string path, Action<GltfImporterTestsBuilder> build)
    {
        ModelRoot model = BuildModel(build);
        string fullPath = CreateFolderFor(path);
        model.SaveGLB(fullPath);
        return this;
    }

    /// <summary>Writes a .gltf with its buffers and images as files beside it.</summary>
    public ModelImporterTestsBuilder WithGltf(string path, Action<GltfImporterTestsBuilder> build)
    {
        ModelRoot model = BuildModel(build);
        string fullPath = CreateFolderFor(path);
        model.SaveGLTF(fullPath);
        return this;
    }

    public AssetBuilder Build() => new([new ModelImporter()]);

    public BuildReport BuildAll(AssetBuilder builder) => _files.BuildAll(builder);

    /// <summary>Replaces every PNG in the source folder with the given one.</summary>
    public void OverwriteImages(string base64Png)
    {
        byte[] png = Convert.FromBase64String(base64Png);
        string[] images = Directory.GetFiles(_files.SourceDirectory, "*.png");
        foreach (string image in images)
        {
            File.WriteAllBytes(image, png);
        }
    }

    public ModelData ReadOutput(string path)
    {
        string fullPath = Path.Combine(_files.OutputDirectory, path);
        using FileStream stream = File.OpenRead(fullPath);
        return ModelFile.Read(stream);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private string CreateFolderFor(string path)
    {
        string fullPath = Path.Combine(_files.SourceDirectory, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        return fullPath;
    }

    private static ModelRoot BuildModel(Action<GltfImporterTestsBuilder> build)
    {
        GltfImporterTestsBuilder model = new();
        build(model);
        return model.BuildModel();
    }
}
