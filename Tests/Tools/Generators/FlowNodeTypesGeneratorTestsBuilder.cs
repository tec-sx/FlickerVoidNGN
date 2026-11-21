using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using FlickerVoid.Engine.Flow.Graphs;

namespace FlickerVoid.Tools.Generators.Tests;

/// <summary>Compiles C# source against the engine's flow module with the generator, and gives the generated text and the compiler's errors.</summary>
internal sealed class FlowNodeTypesGeneratorTestsBuilder
{
    private readonly List<string> _sources = [];

    public FlowNodeTypesGeneratorTestsBuilder WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    public (string Generated, IReadOnlyList<Diagnostic> Errors) Run()
    {
        IEnumerable<SyntaxTree> trees = _sources.Select(static source => CSharpSyntaxTree.ParseText(source));
        string[] assemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        List<MetadataReference> references = [.. assemblies.Select(static path => MetadataReference.CreateFromFile(path))];
        references.Add(MetadataReference.CreateFromFile(typeof(FlowNode).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(FlickerVoid.Engine.Runtime.Data.DataTypes).Assembly.Location));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Game.Gameplay",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FlowNodeTypesGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updated, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();
        string generated = result.GeneratedTrees.Single().ToString();
        IReadOnlyList<Diagnostic> errors = [.. updated.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        return (generated, errors);
    }
}
