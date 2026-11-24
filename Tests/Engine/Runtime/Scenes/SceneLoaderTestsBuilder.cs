using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Runtime.Tests.Content;
using FlickerVoid.Engine.Runtime.Tests.Entities;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

/// <summary>Builds the scene loader under test over scene files in a temporary content folder, a new world and a spawner that knows transforms, glows and save ids.</summary>
internal sealed class SceneLoaderTestsBuilder : IDisposable
{
    private readonly ContentManagerTestsBuilder _files = new();
    private readonly ILogger<SceneLoader> _logger = Substitute.For<ILogger<SceneLoader>>();
    private ContentManager? _content;

    public SceneLoaderTestsBuilder()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    public World World { get; } = World.Create();

    /// <summary>The warnings the loader logged.</summary>
    public IEnumerable<object?[]> Warnings => _logger.ReceivedCalls()
        .Where(static call => call.GetMethodInfo().Name == nameof(ILogger.Log))
        .Select(static call => call.GetArguments())
        .Where(static arguments => arguments[0] is LogLevel.Warning);

    public SceneLoaderTestsBuilder WithScene(string path, string json)
    {
        _files.WithFile(path, json);
        return this;
    }

    public SceneLoader Build()
    {
        _content = new ContentManager(_files.RootDirectory);
        return new SceneLoader(CreateSpawner(), _content, World, _logger);
    }

    /// <summary>Rewrites the file and reports it changed, as the content watcher would.</summary>
    public void ChangeScene(string path, string json)
    {
        _files.WithFile(path, json);
        _content!.NotifyChanged(path);
    }

    /// <summary>Deletes the file and reports it changed, as the content watcher would.</summary>
    public void DeleteScene(string path)
    {
        _content!.Delete(path);
    }

    /// <summary>The transform of the first entity of the scene file, spawned into a world of its own.</summary>
    public Transform LoadInNewWorld(string path)
    {
        World world = World.Create();
        try
        {
            SceneLoader loader = new(CreateSpawner(), new ContentManager(_files.RootDirectory), world, _logger);
            IReadOnlyList<Entity> entities = loader.Load(path);
            return world.Get<Transform>(entities[0]);
        }
        finally
        {
            World.Destroy(world);
        }
    }

    public string ReadFile(string path)
    {
        string fullPath = Path.Combine(_files.RootDirectory, path);
        return File.ReadAllText(fullPath);
    }

    /// <summary>A component of an entity in the scene file as compact JSON.</summary>
    public string ReadComponent(string path, int entity, string component)
    {
        string fullPath = Path.Combine(_files.RootDirectory, path);
        string json = File.ReadAllText(fullPath);
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        JsonElement data = scene.Entities[entity].Components[component];
        return JsonSerializer.Serialize(data, RuntimeJsonContext.Default.JsonElement);
    }

    public void Dispose()
    {
        World.Destroy(World);
        _files.Dispose();
    }

    private static SceneSpawner CreateSpawner()
    {
        SceneSpawner spawner = new();
        spawner.Register("transform", RuntimeJsonContext.Default.Transform);
        spawner.Register("glow", TestEntitiesJsonContext.Default.Glow);
        spawner.Register("saveable", RuntimeJsonContext.Default.Saveable);
        return spawner;
    }
}
