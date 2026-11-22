using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Resources.Knowledge.Scanning;

/// <summary>
/// Builds a playing game with FlickerVoid's gameplay in a 100 by 100 pixel window, a floor, a camera looking straight down with the mouse in
/// the window's centre, and a scanning protagonist at the origin; scan files go in a temporary content folder.
/// </summary>
internal sealed class ScannerSystemTestsBuilder : IDisposable
{
    private const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 10, 0], "rotation": [-0.7071068, 0, 0, 0.7071068] },
          "camera": { "projection": "Orthographic", "orthographicHeight": 10 } } }
        """;

    private const string Floor = """
        { "name": "Floor", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [30, 0.2, 30] } } } }
        """;

    private const string HeroJson = """
        { "name": "Hero", "components": { "transform": {}, "protagonist": {}, "scanner": { "range": 8 } } }
        """;

    private readonly string _folder;
    private DataAssets? _data;
    private Entity _camera;

    public ScannerSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Hero { get; private set; }

    public ScannerSystem Scanner => Game.Services.GetService<ScannerSystem>()!;

    public ScannerSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public ScannerSystemTestsBuilder Build()
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
        Game.AddFlow();
        FlowGraphs graphs = new(content, Game.Data);
        Game.Services.AddService(graphs);
        GameplayModule gameplay = new();
        gameplay.Add(Game);
        Game.Lifecycle.Play(Game.World);
        _camera = Spawn(Camera);
        Spawn(Floor);
        Hero = Spawn(HeroJson);
        return this;
    }

    /// <summary>Adds a box 1 metre wide at <paramref name="position"/> that scans as the given file says.</summary>
    public Entity AddScannable(Vector3 position, string definition)
    {
        string json = $$"""
            { "name": "Crate", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] },
              "staticBody": { "collider": { "type": "box", "offset": [0, 0.5, 0] } }, "scannable": { "definition": "{{definition}}" } } }
            """;
        return Spawn(json);
    }

    /// <summary>Adds a wall 3 metres high and 4 wide across X at <paramref name="z"/>.</summary>
    public void AddWall(float z)
    {
        string json = $$"""
            { "name": "Wall", "components": { "transform": { "position": [0, 0, {{z}}] },
              "staticBody": { "collider": { "type": "box", "size": [4, 3, 0.2], "offset": [0, 1.5, 0] } } } }
            """;
        Spawn(json);
    }

    /// <summary>Moves the camera over <paramref name="point"/>, so the mouse points at it.</summary>
    public void PointAt(Vector3 point)
    {
        ref Transform transform = ref Game.World.Get<Transform>(_camera);
        transform.Position = new Vector3(point.X, 10, point.Z);
    }

    public void Press(Tag action) => Send(action, InputPhase.Pressed);

    public void Release(Tag action) => Send(action, InputPhase.Released);

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

    private void Send(Tag action, InputPhase phase)
    {
        Game.Events.Send(new InputActionEvent(action, phase));
        Game.Events.Dispatch();
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Game.Tick(Game.Loop.FixedStep);
        return spawned[0];
    }
}
