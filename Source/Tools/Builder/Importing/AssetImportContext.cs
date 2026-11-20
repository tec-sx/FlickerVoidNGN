namespace FlickerVoid.Tools.Builder.Importing;

/// <summary>One file's import: where its source is, and what it reads and writes, which the build cache records.</summary>
public sealed class AssetImportContext
{
    private readonly string _sourceDirectory;
    private readonly string _outputDirectory;
    private readonly HashSet<string> _dependencies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _outputs = new(StringComparer.Ordinal);

    public AssetImportContext(string sourceDirectory, string outputDirectory, string sourcePath)
    {
        _sourceDirectory = sourceDirectory;
        _outputDirectory = outputDirectory;
        SourcePath = sourcePath;
        SourceFullPath = AssetPaths.ToFullPath(sourceDirectory, sourcePath);
    }

    /// <summary>The source file's path under the source folder, with forward slashes.</summary>
    public string SourcePath { get; }

    public string SourceFullPath { get; }

    /// <summary>Paths under the source folder of the files the import read besides its source.</summary>
    public IReadOnlyCollection<string> Dependencies => _dependencies;

    /// <summary>Paths under the output folder of the files the import wrote.</summary>
    public IReadOnlyCollection<string> Outputs => _outputs;

    /// <summary>Records a file the import reads besides its source, so the file is built again when it changes.</summary>
    public void AddDependency(string fullPath)
    {
        string path = AssetPaths.ToRelativePath(_sourceDirectory, fullPath);
        _dependencies.Add(path);
    }

    /// <summary>Records an output file and returns its full path, creating its folder.</summary>
    public string GetOutputPath(string path)
    {
        string fullPath = AssetPaths.ToFullPath(_outputDirectory, path);
        string relativePath = AssetPaths.ToRelativePath(_outputDirectory, fullPath);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        _outputs.Add(relativePath);
        return fullPath;
    }
}
