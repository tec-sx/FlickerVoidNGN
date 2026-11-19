using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// Spawns scene files into a world and keeps them in step with the files: when the content manager reports a loaded scene
/// changed, the spawner patches its entities. A change that can't be read is logged, and the world keeps the last good version.
/// </summary>
public sealed partial class SceneLoader : IDisposable
{
    private readonly ISceneSpawner _spawner;
    private readonly IContentManager _content;
    private readonly World _world;
    private readonly ILogger<SceneLoader> _logger;
    private readonly Dictionary<string, LoadedScene> _loaded = [];

    public SceneLoader(ISceneSpawner spawner, IContentManager content, World world, ILogger<SceneLoader> logger)
    {
        _spawner = spawner;
        _content = content;
        _world = world;
        _logger = logger;
        _content.Changed += HandleChanged;
    }

    /// <summary>Spawns the scene file at <paramref name="path"/> and returns its entities as spawned.</summary>
    public IReadOnlyList<Entity> Load(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_loaded.ContainsKey(fullPath))
        {
            throw new InvalidOperationException($"{path} is already loaded.");
        }

        SceneData scene = _content.Load(path, RuntimeJsonContext.Default.SceneData);
        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);
        _loaded.Add(fullPath, new LoadedScene(path, scene, entities));
        return entities;
    }

    /// <summary>Destroys the entities spawned from the scene file and stops following it; does nothing when it isn't loaded.</summary>
    public void Unload(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (!_loaded.Remove(fullPath, out LoadedScene? loaded))
        {
            return;
        }

        foreach (Entity entity in loaded.Entities)
        {
            if (_world.IsAlive(entity))
            {
                _world.Destroy(entity);
            }
        }

        _content.Unload(path);
    }

    /// <summary>
    /// Writes the loaded scene file at <paramref name="path"/> as the world has its entities now (see
    /// <see cref="ISceneSpawner.Capture"/>), then reports the file changed so caches of it reload.
    /// </summary>
    public void Save(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (!_loaded.TryGetValue(fullPath, out LoadedScene? loaded))
        {
            throw new InvalidOperationException($"{path} is not loaded.");
        }

        SceneData scene = _spawner.Capture(loaded.Scene, loaded.Entities, _world);
        using (StreamWriter writer = File.CreateText(fullPath))
        {
            Write(writer, scene);
        }

        _content.NotifyChanged(path);
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
    }

    private void HandleChanged(string fullPath)
    {
        if (!_loaded.TryGetValue(fullPath, out LoadedScene? loaded))
        {
            return;
        }

        try
        {
            SceneData scene = _content.Load(loaded.Path, RuntimeJsonContext.Default.SceneData);
            IReadOnlyList<Entity> entities = _spawner.Patch(loaded.Scene, scene, loaded.Entities, _world);
            _loaded[fullPath] = loaded with { Scene = scene, Entities = entities };
            LogReloaded(loaded.Path);
        }
        catch (InvalidDataException exception)
        {
            _content.Unload(loaded.Path);
            LogInvalid(loaded.Path, exception.Message);
        }
    }

    /// <summary>Writes one entity per line, as scene files are written by hand, so a changed entity is a one-line diff.</summary>
    private static void Write(StreamWriter writer, SceneData scene)
    {
        writer.WriteLine("{");
        if (scene.Schema is not null)
        {
            string schema = JsonSerializer.Serialize(scene.Schema, RuntimeJsonContext.Default.String);
            writer.WriteLine($"  \"{JsonSchemas.SchemaProperty}\": {schema},");
        }

        writer.WriteLine("  \"entities\": [");
        for (int index = 0; index < scene.Entities.Count; index++)
        {
            string entity = JsonSerializer.Serialize(scene.Entities[index], RuntimeJsonContext.Default.EntityData);
            string separator = ",";
            if (index == scene.Entities.Count - 1)
            {
                separator = "";
            }

            writer.WriteLine($"    {entity}{separator}");
        }

        writer.WriteLine("  ]");
        writer.WriteLine("}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloaded {Path}.")]
    private partial void LogReloaded(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kept the last version of {Path}: {Problem}")]
    private partial void LogInvalid(string path, string problem);

    private sealed record LoadedScene(string Path, SceneData Scene, IReadOnlyList<Entity> Entities);
}
