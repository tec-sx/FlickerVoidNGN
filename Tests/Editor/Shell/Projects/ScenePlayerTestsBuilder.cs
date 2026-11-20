using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Tests.Scenes;

namespace FlickerVoid.Editor.Tests.Projects;

/// <summary>Builds a scene player over an edited scene in a temporary content folder, with a launcher that only records what it starts.</summary>
internal sealed class ScenePlayerTestsBuilder : IDisposable
{
    public const string Program = "/game/Game.dll";

    public EditorSceneTestsBuilder Scenes { get; } = new();

    public IGameLauncher Launcher { get; } = Substitute.For<IGameLauncher>();

    public ScenePlayer Build() => new(Scenes.Scene, Program, Launcher);

    public void Dispose() => Scenes.Dispose();
}
