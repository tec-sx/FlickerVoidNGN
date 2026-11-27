using FlickerVoid.Tools.Builder.Importing;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Tools.Builder.Models;

/// <summary>
/// Bakes a glTF model (.glb, or .gltf with the buffers and images it names) into <c>Name.fvmodel</c>, which the game loads
/// without parsing glTF or decoding images.
/// </summary>
public sealed class ModelImporter : IAssetImporter
{
    public string Name => "Model";

    public int Version => 3;

    public IReadOnlyList<string> Extensions { get; } = [".glb", ".gltf"];

    public void Import(AssetImportContext context)
    {
        IReadOnlyList<string> files = GltfImporter.FindFiles(context.SourceFullPath);
        foreach (string file in files.Skip(1))
        {
            context.AddDependency(file);
        }

        ModelData model = GltfImporter.Load(context.SourceFullPath);
        string outputPath = Path.ChangeExtension(context.SourcePath, ".fvmodel");
        string fullOutputPath = context.GetOutputPath(outputPath);
        using FileStream output = File.Create(fullOutputPath);
        ModelFile.Write(model, output);
    }
}
