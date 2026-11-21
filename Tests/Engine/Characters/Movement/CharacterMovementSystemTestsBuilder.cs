using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Characters.Tests.Movement;

/// <summary>Builds a game with characters, and physics when asked, spawning entities written as scene JSON and stepping its fixed updates.</summary>
internal sealed class CharacterMovementSystemTestsBuilder : IDisposable
{
    private bool _physics;

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public CharacterMovementSystemTestsBuilder WithPhysics()
    {
        _physics = true;
        return this;
    }

    public Game Build()
    {
        if (_physics)
        {
            Game.AddPhysics();
        }

        Game.AddCharacters();
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
    }
}
