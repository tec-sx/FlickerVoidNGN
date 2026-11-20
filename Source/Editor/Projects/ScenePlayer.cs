using FlickerVoid.Engine.Editor.Scenes;

namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>
/// Plays the open scene in the project's game, as the world is now and without saving it: the world is written to
/// <c>.play/&lt;scene&gt;</c> in the built content folder, outside the source folders, and the game runs it with <c>--scene</c>.
/// </summary>
public sealed class ScenePlayer(EditorScene scene, string program, IGameLauncher launcher)
{
    public const string PlayFolder = ".play";

    /// <summary>Starts the game on the world; false when the world has no scene file to play.</summary>
    public bool Play()
    {
        if (scene.Capture() is not { } contents)
        {
            return false;
        }

        string fileName = Path.GetFileName(scene.ScenePath)!;
        string path = $"{PlayFolder}/{fileName}";
        string fullPath = scene.Content.GetFullPath(path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(fullPath, contents);
        string[] arguments = ["--scene", path];
        launcher.Launch(program, arguments);
        return true;
    }
}
