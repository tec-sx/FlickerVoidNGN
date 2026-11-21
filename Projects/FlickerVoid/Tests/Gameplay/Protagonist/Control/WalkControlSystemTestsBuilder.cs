using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Flow;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Control;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

/// <summary>
/// Builds a playing game with FlickerVoid's gameplay, input actions whose map binds W, A, S and D to <see cref="WalkControlSystem.Move"/>,
/// and keys held down on request.
/// </summary>
internal sealed class WalkControlSystemTestsBuilder : IDisposable
{
    /// <summary>A camera behind the origin looking down at it, as the demo's: tilted 30 degrees, facing -Z.</summary>
    public const string Camera = """
        { "name": "Camera", "components": { "transform": { "position": [0, 6, 9], "rotation": [-0.2588, 0, 0, 0.9659] },
          "camera": { "projection": "Orthographic", "orthographicHeight": 6.5 } } }
        """;

    public const string Fox = """
        { "name": "Fox", "components": { "transform": {}, "protagonist": {}, "characterMover": { "speed": 2 }, "interactor": {} } }
        """;

    private readonly string _folder;
    private readonly HashSet<Key> _down = [];
    private DataAssets? _data;

    public WalkControlSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Game Build()
    {
        IGameWindow window = Substitute.For<IGameWindow>();
        window.Size.Returns(new Vector2D<int>(100, 100));
        IInputState input = Substitute.For<IInputState>();
        input.IsDown(Arg.Any<Key>()).Returns(call => _down.Contains(call.Arg<Key>()));
        Game.Services.AddService(window);
        Game.Services.AddService(input);
        InputActions actions = Game.AddInputActions(input);
        actions.Map = CreateMap();
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
        return Game;
    }

    public IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World);
    }

    public void Hold(Key key) => _down.Add(key);

    public void Release(Key key) => _down.Remove(key);

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

    private static InputMap CreateMap()
    {
        InputActionDefinition move = new() { Id = WalkControlSystem.Move };
        move.Bindings.Add(new InputBinding { Key = Key.W, Value = new Vector2(0, 1) });
        move.Bindings.Add(new InputBinding { Key = Key.S, Value = new Vector2(0, -1) });
        move.Bindings.Add(new InputBinding { Key = Key.A, Value = new Vector2(-1, 0) });
        move.Bindings.Add(new InputBinding { Key = Key.D, Value = new Vector2(1, 0) });
        InputMap map = new();
        map.Actions.Add(move);
        return map;
    }
}
