namespace FlickerVoid.Engine.Builder.Importing;

/// <summary>Leaves files out of the output, such as HLSL that shader imports read.</summary>
public sealed class SkipImporter(params string[] extensions) : IAssetImporter
{
    public string Name => "Skip";

    public int Version => 1;

    public IReadOnlyList<string> Extensions { get; } = extensions;

    public void Import(AssetImportContext context)
    {
    }
}
