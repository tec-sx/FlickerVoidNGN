namespace FlickerVoid.Engine.Builder.Importing;

/// <summary>Copies a file to the same path in the output; the builder uses it for files no other importer takes.</summary>
public sealed class CopyImporter : IAssetImporter
{
    public string Name => "Copy";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = [];

    public void Import(AssetImportContext context)
    {
        string outputPath = context.GetOutputPath(context.SourcePath);
        File.Copy(context.SourceFullPath, outputPath, overwrite: true);
    }
}
