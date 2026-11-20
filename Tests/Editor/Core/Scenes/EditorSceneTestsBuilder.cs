using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

/// <summary>Builds the edited scene over scene files in a temporary content folder.</summary>
internal sealed class EditorSceneTestsBuilder : IDisposable
{
    private readonly string _contentFolder;

    public EditorSceneTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        Scene = new EditorScene(NullLoggerFactory.Instance, new ContentManager(_contentFolder));
    }

    public EditorScene Scene { get; }

    public EditorSceneTestsBuilder WithScene(string path, string json)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Opens the scene file and returns its first entity.</summary>
    public Entity Open(string path)
    {
        Scene.Open(path);
        Entity first = default;
        bool found = false;
        Scene.Game.World.Query(new QueryDescription(), (Entity entity) =>
        {
            if (!found)
            {
                first = entity;
                found = true;
            }
        });
        return first;
    }

    /// <summary>The component scene files add under the name.</summary>
    public SceneComponent FindComponent(string name)
    {
        return Scene.Game.Spawner.Components.Single(component => component.Name == name);
    }

    public string ReadScene(string path)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        return File.ReadAllText(fullPath).ReplaceLineEndings("\n");
    }

    public void Dispose()
    {
        Scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }
}
