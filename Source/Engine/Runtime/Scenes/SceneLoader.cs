using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// Spawns scene files into a world and keeps them in step with the files: when the content manager reports a loaded scene
/// changed, the spawner patches its entities, and likewise when an entity template they use changes. A change that can't be read is
/// logged, and the world keeps the last good version.
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
        Templates = new EntityTemplates(content, spawner);
        _content.Changed += HandleChanged;
    }

    /// <summary>The entity templates the scenes start their entities from, which game code spawns too.</summary>
    public EntityTemplates Templates { get; }

    /// <summary>Spawns the scene file at <paramref name="path"/> and returns its entities as spawned.</summary>
    public IReadOnlyList<Entity> Load(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (_loaded.ContainsKey(fullPath))
        {
            throw new InvalidOperationException($"{path} is already loaded.");
        }

        SceneData scene = Read(path);
        IReadOnlyList<Entity> entities = _spawner.Spawn(scene, _world);
        _loaded.Add(fullPath, new LoadedScene(path, scene, entities));
        return entities;
    }

    /// <summary>
    /// Creates an entity in the loaded scene file at <paramref name="path"/>, as if the file had it last: it is saved with the
    /// scene and matched by later changes to the file.
    /// </summary>
    public Entity Add(string path, EntityData data)
    {
        LoadedScene loaded = GetLoaded(path, out string fullPath);
        EntityData expanded = Templates.Expand(data);
        Entity entity = _spawner.Create(expanded, _world);
        SceneData scene = new() { Schema = loaded.Scene.Schema, Entities = [.. loaded.Scene.Entities, expanded] };
        List<Entity> entities = [.. loaded.Entities, entity];
        _loaded[fullPath] = loaded with { Scene = scene, Entities = entities };
        return entity;
    }

    /// <summary>The entities spawned from the loaded scene file at <paramref name="path"/>, destroyed ones included.</summary>
    public IReadOnlyList<Entity> GetEntities(string path)
    {
        return GetLoaded(path, out _).Entities;
    }

    /// <summary>The template the loaded scene file at <paramref name="path"/> gave the entity, or null.</summary>
    public string? GetTemplate(string path, Entity entity)
    {
        LoadedScene loaded = GetLoaded(path, out _);
        for (int index = 0; index < loaded.Entities.Count; index++)
        {
            if (loaded.Entities[index] == entity)
            {
                return loaded.Scene.Entities[index].Template;
            }
        }

        return null;
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
                _spawner.Lifecycle.Destroy(_world, entity);
            }
        }

        _content.Unload(path);
    }

    /// <summary>
    /// Writes the loaded scene file at <paramref name="path"/> as the world has its entities now (see
    /// <see cref="ISceneSpawner.Capture"/>), through the content manager, which also writes its source file in a development build and reports it changed.
    /// </summary>
    public void Save(string path)
    {
        byte[] contents = Capture(path);
        _content.Write(path, contents);
    }

    /// <summary>The loaded scene file at <paramref name="path"/> as <see cref="Save"/> would write it, without writing it.</summary>
    public byte[] Capture(string path)
    {
        string fullPath = _content.GetFullPath(path);
        if (!_loaded.TryGetValue(fullPath, out LoadedScene? loaded))
        {
            throw new InvalidOperationException($"{path} is not loaded.");
        }

        SceneData captured = _spawner.Capture(loaded.Scene, loaded.Entities, _world);
        SceneData scene = new() { Schema = captured.Schema, Entities = [.. captured.Entities.Select(Templates.Reduce)] };
        StringWriter writer = new();
        Write(writer, scene);
        string text = writer.ToString();
        return Encoding.UTF8.GetBytes(text);
    }

    private LoadedScene GetLoaded(string path, out string fullPath)
    {
        fullPath = _content.GetFullPath(path);
        if (!_loaded.TryGetValue(fullPath, out LoadedScene? loaded))
        {
            throw new InvalidOperationException($"{path} is not loaded.");
        }

        return loaded;
    }

    public void Dispose()
    {
        _content.Changed -= HandleChanged;
    }

    private void HandleChanged(string fullPath)
    {
        if (_loaded.TryGetValue(fullPath, out LoadedScene? loaded))
        {
            Reload(fullPath, loaded);
            return;
        }

        if (!fullPath.EndsWith(EntityTemplates.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach ((string scenePath, LoadedScene scene) in _loaded.ToList())
        {
            if (UsesTemplate(scene, fullPath))
            {
                Reload(scenePath, scene);
            }
        }
    }

    private bool UsesTemplate(LoadedScene loaded, string fullPath)
    {
        try
        {
            return loaded.Scene.Entities.Any(entity => Templates.Uses(entity, fullPath));
        }
        catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException or DirectoryNotFoundException)
        {
            LogInvalid(loaded.Path, exception.Message);
            return false;
        }
    }

    /// <summary>The scene file with each entity's template expanded under its own values.</summary>
    private SceneData Read(string path)
    {
        SceneData file = _content.Load(path, RuntimeJsonContext.Default.SceneData);
        return new SceneData { Schema = file.Schema, Entities = [.. file.Entities.Select(Templates.Expand)] };
    }

    private void Reload(string fullPath, LoadedScene loaded)
    {
        try
        {
            SceneData scene = Read(loaded.Path);
            IReadOnlyList<Entity> entities = _spawner.Patch(loaded.Scene, scene, loaded.Entities, _world);
            _loaded[fullPath] = loaded with { Scene = scene, Entities = entities };
            LogReloaded(loaded.Path);
        }
        catch (Exception exception) when (exception is InvalidDataException or FileNotFoundException or DirectoryNotFoundException)
        {
            _content.Unload(loaded.Path);
            LogInvalid(loaded.Path, exception.Message);
        }
    }

    /// <summary>Writes one entity per line, as scene files are written by hand, so a changed entity is a one-line diff.</summary>
    private static void Write(StringWriter writer, SceneData scene)
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
