using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlickerVoid.Tools.Generators.Tests;

/// <summary>Compiles C# source against the engine's runtime with the entity class generator, and gives the generated text and the compiler's and generator's errors.</summary>
internal sealed class EntityClassGeneratorTestsBuilder
{
    private readonly List<string> _sources = [];

    public EntityClassGeneratorTestsBuilder WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    public (string Generated, IReadOnlyList<Diagnostic> Errors) Run()
    {
        CSharpParseOptions options = new(LanguageVersion.Latest);
        IEnumerable<SyntaxTree> trees = _sources.Select(source => CSharpSyntaxTree.ParseText(source, options));
        string[] platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        string[] engine = [typeof(Engine.Runtime.Entities.EntityClassAttribute).Assembly.Location, typeof(Arch.Core.World).Assembly.Location];
        List<MetadataReference> references = [.. platform.Concat(engine).Distinct().Select(static path => MetadataReference.CreateFromFile(path))];
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Game.Gameplay",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new EntityClassGenerator().AsSourceGenerator()], parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation updated, out System.Collections.Immutable.ImmutableArray<Diagnostic> reported);
        GeneratorDriverRunResult result = driver.GetRunResult();
        string generated = string.Join("\n", result.GeneratedTrees.Select(static tree => tree.ToString()));
        IEnumerable<Diagnostic> compiled = updated.GetDiagnostics();
        IReadOnlyList<Diagnostic> errors = [.. reported.Concat(compiled).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        return (generated, errors);
    }
}
