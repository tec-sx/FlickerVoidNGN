namespace FlickerVoid.Tools.Builder.Importing;

/// <summary>Turns one kind of source file into the files the game loads.</summary>
public interface IAssetImporter
{
    /// <summary>Names the importer in the build cache.</summary>
    string Name { get; }

    /// <summary>Raise it when the importer's output changes, so files built by an older version are built again.</summary>
    int Version { get; }

    /// <summary>The endings of the file names this importer takes, such as ".png" or ".fvshader".</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>Writes the outputs of <see cref="AssetImportContext.SourcePath"/>; throws when the file can't be imported.</summary>
    void Import(AssetImportContext context);
}
