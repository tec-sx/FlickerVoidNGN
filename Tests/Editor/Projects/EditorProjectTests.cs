using FlickerVoid.Engine.Editor.Projects;

namespace FlickerVoid.Engine.Editor.Tests.Projects;

public sealed class EditorProjectTests : IDisposable
{
    private readonly EditorProjectTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void BuildsTheGameAndOpensItsBuiltContent()
    {
        string file = _builder.WithProjectFile("Hollow.fvproject", """{ "game": "Source/Launcher/Hollow.Launcher.csproj", "startupScene": "Scenes/Main.fvscene" }""");

        EditorProject project = _builder.Open(file);

        project.Name.Should().Be("Hollow");
        project.Game.Should().Be(Path.Combine(_builder.Folder, "Source", "Launcher", "Hollow.Launcher.csproj"));
        _builder.Built.Should().Equal(project.Game);
        project.Program.Should().Be(_builder.Program);
        project.ContentFolder.Should().Be(Path.Combine(_builder.OutputFolder, "Content"));
        project.StartupScene.Should().Be("Scenes/Main.fvscene");
    }

    [Fact]
    public void AProjectFileWithoutAGameIsInvalid()
    {
        string file = _builder.WithProjectFile("Hollow.fvproject", """{ "startupScene": "Scenes/Main.fvscene" }""");

        Action open = () => _builder.Open(file);

        open.Should().Throw<InvalidDataException>().WithMessage("*Hollow.fvproject*game*");
    }
}
