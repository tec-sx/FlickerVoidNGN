using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Interaction.Tests.Interactors;

/// <summary>Builds a playing game with interaction over data files in a temporary content folder, spawning entities written as scene JSON.</summary>
internal sealed class InteractionSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public InteractionSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InteractionSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Adds interaction and the data assets service, then starts play, so spawned entities activate.</summary>
    public Game Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddInteraction();
        Game.Lifecycle.Play(Game.World);
        return Game;
    }

    public IReadOnlyList<Entity> Spawn(string entitiesJson)
    {
        string json = $$"""{ "entities": [ {{entitiesJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World);
    }

    /// <summary>Sends an event and delivers it, as the game does after each group of systems.</summary>
    public void Send<T>(in T value)
        where T : struct
    {
        Game.Events.Send(value);
        Game.Events.Dispatch();
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
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
