using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

/// <summary>Builds a game compiler over an edited scene, with a project builder that "builds" this test assembly as the game.</summary>
internal sealed class GameCompilerTestsBuilder : IDisposable
{
    public const string GameProject = "/game/Game.csproj";

    public GameCompilerTestsBuilder()
    {
        string program = typeof(GameCompilerTestsBuilder).Assembly.Location;
        Builder.Build(GameProject, null).Returns(program);
    }

    public EditorSceneTestsBuilder Scenes { get; } = new();

    public IProjectBuilder Builder { get; } = Substitute.For<IProjectBuilder>();

    public GameCompiler Build() => new(Scenes.Scene, GameProject, Builder, null);

    public void Dispose() => Scenes.Dispose();
}
