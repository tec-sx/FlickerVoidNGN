using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime.Tests.Saves;

/// <summary>Builds a game with saves in a temporary folder, and can start the game again over the same folder, as a player restarting it.</summary>
internal sealed class SaveSystemTestsBuilder : IDisposable
{
    private readonly string _folder;

    public SaveSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
    }

    public Game Game { get; private set; } = new(NullLoggerFactory.Instance);

    public SaveSystem Saves { get; private set; } = null!;

    public string Folder => _folder;

    public SaveSystemTestsBuilder Build()
    {
        SaveSettings settings = new() { Folder = _folder };
        Saves = Game.AddSaves(settings);
        return this;
    }

    /// <summary>Spawns the scene's entities and starts play.</summary>
    public IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Game.Lifecycle.Play(Game.World);
        return spawned;
    }

    /// <summary>Throws the game away and builds a new one over the same save folder.</summary>
    public SaveSystemTestsBuilder StartAgain()
    {
        Game.Dispose();
        Game = new Game(NullLoggerFactory.Instance);
        return Build();
    }

    /// <summary>Lets the game play for <paramref name="seconds"/>.</summary>
    public void Play(double seconds)
    {
        TimeSpan step = TimeSpan.FromSeconds(0.25);
        for (double passed = 0; passed < seconds; passed += step.TotalSeconds)
        {
            Game.Tick(step);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
