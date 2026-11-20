using System.Text.Json;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Serialization;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// Plays the open scene in the project's game as the viewport shows it, without saving anything: the world and the viewport's
/// lighting mood are written to <c>.play/&lt;scene&gt;.fvscene</c> and <c>.play/&lt;scene&gt;.fvmood</c> in the built content
/// folder, outside the source folders, and the game runs them with <c>--scene</c> and <c>--mood</c>.
/// </summary>
public sealed class ScenePlayer(EditorScene scene, string program, IGameLauncher launcher)
{
    public const string PlayFolder = ".play";

    /// <summary>Starts the game on the world lit by <paramref name="mood"/>; false when the world has no scene file to play.</summary>
    public bool Play(LightingMood mood)
    {
        if (scene.Capture() is not { } contents)
        {
            return false;
        }

        string fileName = Path.GetFileName(scene.ScenePath)!;
        string scenePath = $"{PlayFolder}/{fileName}";
        string moodPath = Path.ChangeExtension(scenePath, ".fvmood");
        byte[] moodContents = JsonSerializer.SerializeToUtf8Bytes(mood, RenderingJsonContext.Default.LightingMood);
        Write(scenePath, contents);
        Write(moodPath, moodContents);
        string[] arguments = ["--scene", scenePath, "--mood", moodPath];
        launcher.Launch(program, arguments);
        return true;
    }

    private void Write(string path, byte[] contents)
    {
        string fullPath = scene.Content.GetFullPath(path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(fullPath, contents);
    }
}
