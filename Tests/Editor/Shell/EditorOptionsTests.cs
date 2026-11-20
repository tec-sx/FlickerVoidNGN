namespace FlickerVoid.Editor.Tests;

public sealed class EditorOptionsTests
{
    [Fact]
    public void ReadsAProjectFileAndAScene()
    {
        EditorOptions? options = EditorOptions.Parse(["Game.fvproject", "--scene", "Scenes/Hall.fvscene"], out string? error);

        error.Should().BeNull();
        options!.Project.Should().Be(Path.GetFullPath("Game.fvproject"));
        options.Scene.Should().Be("Scenes/Hall.fvscene");
    }

    [Fact]
    public void RefusesAProjectAndAContentFolderTogether()
    {
        EditorOptions? options = EditorOptions.Parse(["Game.fvproject", "--content", "Content"], out string? error);

        options.Should().BeNull();
        error.Should().Contain("not both");
    }
}
