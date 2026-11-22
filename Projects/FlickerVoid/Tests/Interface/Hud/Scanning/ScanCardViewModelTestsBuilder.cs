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
using FlickerVoid.Gameplay;
using FlickerVoid.Gameplay.Protagonist.Resources.Knowledge.Scanning;
using FlickerVoid.Interface.Hud.Scanning;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Maths;

namespace FlickerVoid.Interface.Tests.Hud.Scanning;

/// <summary>
/// Builds the scan card over a playing game with FlickerVoid's gameplay in a 100 by 100 pixel window: a camera looking straight down at
/// a crate 3 metres from the scanning protagonist, with the mouse on the crate. The crate's scan takes a second and has four entries.
/// </summary>
internal sealed class ScanCardViewModelTestsBuilder : IDisposable
{
    private const string Scan = """
        { "type": "scan", "display": { "name": "Old crate" }, "scanDuration": 1, "entries": [
          { "label": "Wood", "text": "Old oak." }, { "label": "Marks", "text": "Knife scratches." },
          { "label": "Smell", "text": "Wine." }, { "label": "Weight", "text": "Heavy." } ] }
        """;

    private const string Scene = """
        { "entities": [
          { "name": "Camera", "components": { "transform": { "position": [3, 10, 0], "rotation": [-0.7071068, 0, 0, 0.7071068] },
            "camera": { "projection": "Orthographic", "orthographicHeight": 10 } } },
          { "name": "Fox", "components": { "transform": {}, "protagonist": {}, "scanner": {} } },
          { "name": "Crate", "components": { "transform": { "position": [3, 0, 0] },
            "staticBody": { "collider": { "type": "box", "offset": [0, 0.5, 0] } }, "scannable": { "definition": "Crate.fvdata" } } } ] }
        """;

    private readonly string _folder;
    private DataAssets? _data;

    public ScanCardViewModelTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
        string scan = Path.Combine(_folder, "Crate.fvdata");
        File.WriteAllText(scan, Scan);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Protagonist { get; private set; }

    public ScannerSystem Scanner => Game.Services.GetService<ScannerSystem>()!;

    public ScanCardViewModel Build()
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
        SceneData scene = JsonSerializer.Deserialize(Scene, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Protagonist = spawned[1];
        Run(0.1f);
        return new ScanCardViewModel(Scanner, Game.World);
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
