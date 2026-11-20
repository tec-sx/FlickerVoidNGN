using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

public sealed class DotnetCliTests : IDisposable
{
    private const string EngineFolder = "Engine";

    private static readonly string EditorBinFolder = Path.Combine(EngineFolder, "Source", "Editor", "bin");

    private readonly DotnetCliTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void TheEnvironmentVariableNamesTheEngine()
    {
        string start = _builder.WithFolder(EditorBinFolder);

        string? engine = DotnetCli.FindEngine(@"D:\Engine", start);

        engine.Should().Be(@"D:\Engine");
    }

    [Fact]
    public void WithoutTheVariableTheEngineIsTheFolderAboveWithTheProjectTemplate()
    {
        string engine = _builder.WithFolder(EngineFolder);
        _builder.WithFolder(EngineFolder, DotnetCli.TemplateFolder);
        string start = _builder.WithFolder(EditorBinFolder);

        string? found = DotnetCli.FindEngine("", start);

        found.Should().Be(engine);
    }

    [Fact]
    public void WithoutTheVariableOrATemplateThereIsNoEngine()
    {
        string start = _builder.WithFolder("Elsewhere", "bin");

        string? found = DotnetCli.FindEngine(null, start);

        found.Should().BeNull();
    }

    [Theory]
    [InlineData("MSBuildSDKsPath")]
    [InlineData("MSBuildExtensionsPath")]
    [InlineData("MSBUILD_EXE_PATH")]
    public void StartsDotnetWithoutTheMSBuildPathsOfTheEditorsOwnEnvironment(string variable)
    {
        Environment.SetEnvironmentVariable(variable, @"C:\Program Files\dotnet\sdk.0.300\Sdks");
        try
        {
            System.Diagnostics.ProcessStartInfo start = DotnetCli.CreateStartInfo(["build"]);

            start.Environment.Should().NotContainKey(variable);
            start.Environment[DotnetCli.EngineVariable].Should().Be(DotnetCli.Engine);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }
}
