using FlickerVoid.Tools.Builder.Importing;

namespace FlickerVoid.Tools.Builder.Tests;

/// <summary>
/// Imports .txt files as upper-case .upper files. A line "include &lt;path&gt;" appends that file, read as a dependency, and a file
/// reading "fail" throws.
/// </summary>
internal sealed class TestImporter : IAssetImporter
{
    private const string IncludePrefix = "include ";

    private readonly List<string> _imported = [];

    public string Name => "Test";

    public int Version { get; set; } = 1;

    public IReadOnlyList<string> Extensions { get; } = [".txt"];

    public IReadOnlyList<string> Imported => _imported;

    public void Import(AssetImportContext context)
    {
        _imported.Add(context.SourcePath);
        string[] lines = File.ReadAllLines(context.SourceFullPath);
        if (lines is ["fail"])
        {
            throw new InvalidDataException("The file asks to fail.");
        }

        List<string> output = [];
        foreach (string line in lines)
        {
            if (line.StartsWith(IncludePrefix, StringComparison.Ordinal))
            {
                string folder = Path.GetDirectoryName(context.SourceFullPath)!;
                string included = Path.Combine(folder, line[IncludePrefix.Length..]);
                context.AddDependency(included);
                string includedText = File.ReadAllText(included);
                output.Add(includedText);
            }
            else
            {
                output.Add(line);
            }
        }

        string outputPath = Path.ChangeExtension(context.SourcePath, ".upper");
        string fullOutputPath = context.GetOutputPath(outputPath);
        string text = string.Join('\n', output);
        File.WriteAllText(fullOutputPath, text.ToUpperInvariant());
    }
}
