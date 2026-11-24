using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Shots;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Shots;

/// <summary>
/// Builds an editor scene whose game knows cameras, as a project adding them would, with the shot timeline over its selection;
/// a Hall shot stands a metre along X with keys at 0, 2 and 4 seconds, ten metres apart along its X.
/// </summary>
internal sealed class ShotTimelineViewModelTestsBuilder : IDisposable
{
    public const string HallKeys = """
        { "position": [0, 0, 0], "time": 0 }, { "position": [10, 0, 0], "time": 2 }, { "position": [20, 0, 0], "time": 4 }
        """;

    private readonly string _contentFolder;

    public ShotTimelineViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        string tempPath = Path.GetTempPath();
        _contentFolder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_contentFolder);
        ContentManager content = new(_contentFolder);
        Scene = new EditorScene(NullLoggerFactory.Instance, content, [new CamerasModule()]);
        Timeline = new ShotTimelineViewModel(Scene);
        string scenePath = Path.Combine(_contentFolder, "Room.fvscene");
        File.WriteAllText(scenePath, """{ "entities": [] }""");
        Scene.Open("Room.fvscene");
    }

    public EditorScene Scene { get; }

    public ShotTimelineViewModel Timeline { get; }

    public Entity AddShot(string name, string keys = HallKeys)
    {
        return Add($$"""
            { "name": "{{name}}", "components": { "transform": { "position": [1, 0, 0] }, "camera": { "orthographicHeight": 6 },
              "shot": { "keys": [ {{keys}} ] } } }
            """);
    }

    public Entity Add(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Scene.AddEntity(scene.Entities[0]);
    }

    public void Dispose()
    {
        Scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }
}
