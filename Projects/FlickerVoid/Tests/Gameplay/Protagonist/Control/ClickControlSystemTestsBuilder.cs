using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Control;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

/// <summary>
/// Builds a playing game with FlickerVoid's gameplay in a 100 by 100 pixel window, data files in a temporary content folder, and a
/// camera looking straight down that clicks land under; the mouse stays in the window's centre.
/// </summary>
internal sealed class ClickControlSystemTestsBuilder : IDisposable
{
    public const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 10, 0], "rotation": [-0.7071068, 0, 0, 0.7071068] },
          "camera": { "projection": "Orthographic", "orthographicHeight": 10 } } }
        """;

    public const string Floor = """
        { "name": "Floor", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [20, 0.2, 20] } } } }
        """;

    private readonly string _folder;
    private DataAssets? _data;

    public ClickControlSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public ClickControlSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public Game Build()
    {
        IGameWindow window = Substitute.For<IGameWindow>();
        window.Size.Returns(new Vector2D<int>(100, 100));
        IInputState input = Substitute.For<IInputState>();
        input.MousePosition.Returns(new Vector2(50, 50));
        Game.Services.AddService(window);
        Game.Services.AddService(input);
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddRendering();
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        Game.Lifecycle.Play(Game.World);
        return Game;
    }

    /// <summary>Spawns the entities and runs a step, so physics has their bodies before the first click.</summary>
    public IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Game.Tick(Game.Loop.FixedStep);
        return spawned;
    }

    /// <summary>Clicks under the camera after moving it over <paramref name="point"/>.</summary>
    public void ClickAt(Entity camera, Vector3 point)
    {
        ref Transform transform = ref Game.World.Get<Transform>(camera);
        transform.Position = new Vector3(point.X, 10, point.Z);
        Game.Events.Send(new InputActionEvent(ClickControlSystem.Use, InputPhase.Pressed));
        Game.Events.Dispatch();
    }

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
