using System.Text.Json;

using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds a game with rendering added, to spawn entities from scene file JSON.</summary>
internal sealed class SceneMoodTestsBuilder : IDisposable
{
    public SceneMoodTestsBuilder()
    {
        Game = new Game(NullLoggerFactory.Instance);
        Game.AddRendering();
    }

    public Game Game { get; }

    public void Spawn(string entityJson)
    {
        string sceneJson = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(sceneJson, RuntimeJsonContext.Default.SceneData)!;
        Game.Spawner.Spawn(scene, Game.World);
    }

    public void Dispose() => Game.Dispose();
}
