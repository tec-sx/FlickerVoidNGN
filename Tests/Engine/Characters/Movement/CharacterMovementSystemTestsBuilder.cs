using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Characters.Movement;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Characters.Tests.Movement;

/// <summary>
/// Builds a game with characters and saves in a temporary folder, and physics and a path finder when asked, spawning entities written as
/// scene JSON and stepping its fixed updates.
/// </summary>
internal sealed class CharacterMovementSystemTestsBuilder : IDisposable
{
    private bool _physics;
    private IPathFinder? _paths;
    private readonly string _saves = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public SaveSystem Saves { get; private set; } = null!;

    public CharacterMovementSystemTestsBuilder WithPhysics()
    {
        _physics = true;
        return this;
    }

    public CharacterMovementSystemTestsBuilder WithPathFinder(IPathFinder paths)
    {
        _paths = paths;
        return this;
    }

    public Game Build()
    {
        if (_physics)
        {
            Game.AddPhysics();
        }

        CharacterMovementSystem movement = Game.AddCharacters();
        movement.PathFinder = _paths;
        Saves = Game.AddSaves(new SaveSettings { Folder = _saves });
        return Game;
    }

    public IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World);
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
        if (Directory.Exists(_saves))
        {
            Directory.Delete(_saves, recursive: true);
        }
    }
}
