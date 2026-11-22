using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Gameplay.Protagonist.Control;

using Microsoft.Extensions.Logging.Abstractions;

using Silk.NET.Input;
using Silk.NET.Maths;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

/// <summary>
/// Builds a playing game with cameras, input actions whose map binds Space to <see cref="CameraControlSystem.Skip"/>, a 200 by
/// 100 pixel window with the mouse where it is put, and the camera control system.
/// </summary>
internal sealed class CameraControlSystemTestsBuilder : IDisposable
{
    private readonly HashSet<Key> _down = [];
    private Vector2 _mouse;

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public CameraDirector Director { get; private set; } = null!;

    public CameraControlSystemTestsBuilder Build()
    {
        IGameWindow window = Substitute.For<IGameWindow>();
        window.Size.Returns(new Vector2D<int>(200, 100));
        IInputState input = Substitute.For<IInputState>();
        input.IsDown(Arg.Any<Key>()).Returns(call => _down.Contains(call.Arg<Key>()));
        input.MousePosition.Returns(_ => _mouse);
        InputActions actions = Game.AddInputActions(input);
        actions.Map = CreateMap();
        Game.AddRendering();
        Director = Game.AddCameras();
        CameraControlSystem control = new(Game.World, actions, input, window, Director);
        Game.FrameSystems.Add(control);
        Game.Lifecycle.Play(Game.World);
        return this;
    }

    public Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }

    public void PutMouse(float x, float y) => _mouse = new Vector2(x, y);

    public void Hold(Key key) => _down.Add(key);

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose() => Game.Dispose();

    private static InputMap CreateMap()
    {
        InputActionDefinition skip = new() { Id = CameraControlSystem.Skip };
        skip.Bindings.Add(new InputBinding { Key = Key.Space });
        InputMap map = new();
        map.Actions.Add(skip);
        return map;
    }
}
