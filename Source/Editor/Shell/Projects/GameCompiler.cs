using FlickerVoid.Editor.Core.Projects;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// Compiles the project's game again and reloads its code into the edited scene, after Unreal's Live Coding: the build runs off
/// the UI thread, then the built game is loaded, the scene spawned again with it (keeping its unsaved edits) and the old code
/// unloaded.
/// </summary>
public sealed class GameCompiler(EditorScene scene, string gameProject, IProjectBuilder builder, GameCode? code)
{
    /// <summary>The code the scene's game is made with; null while the project's code never loaded.</summary>
    public GameCode? Code { get; private set; } = code;

    /// <summary>
    /// Builds the game and reloads its code, returning how many game modules it holds; throws <see cref="ProjectBuildException"/>
    /// when the build fails and what <see cref="EditorScene.Reload"/> throws when the scene can't be spawned with the new code,
    /// leaving the scene as it was.
    /// </summary>
    public async Task<int> Compile()
    {
        string program = await Task.Run(() => builder.Build(gameProject));
        GameCode next = GameCode.Load(program);
        try
        {
            scene.Reload(next.Modules);
        }
        catch
        {
            next.Unload();
            throw;
        }

        GameCode? previous = Code;
        Code = next;
        previous?.Unload();
        return next.Modules.Count;
    }
}
