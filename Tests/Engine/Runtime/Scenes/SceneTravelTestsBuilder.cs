using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Tests.Content;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime.Tests.Scenes;

/// <summary>
/// Builds a game that travels between scene files in a temporary content folder, with saves in a temporary folder: the street, with a
/// crate, a key and the fox, and the cellar, with its own crate, an entry by the stairs and the fox again.
/// </summary>
internal sealed class SceneTravelTestsBuilder : IDisposable
{
    public const string Street = "Scenes/Street.fvscene";
    public const string Cellar = "Scenes/Cellar.fvscene";

    private const string StreetJson = """
        { "entities": [
            { "name": "Crate", "components": { "transform": { "position": [1, 0, 0] }, "saveable": { "id": "Crate" } } },
            { "name": "Key", "components": { "transform": { "position": [2, 0, 0] }, "saveable": { "id": "Key" } } },
            { "name": "Fox", "components": { "transform": { "position": [3, 0, 0] }, "saveable": { "id": "Fox" }, "traveller": {} } }
        ] }
        """;

    private const string CellarJson = """
        { "entities": [
            { "name": "Barrel", "components": { "transform": { "position": [7, 0, 0] }, "saveable": { "id": "Barrel" } } },
            { "name": "Stairs", "components": { "transform": { "position": [9, -3, 0], "rotation": [0, 1, 0, 0] } } },
            { "name": "Fox", "components": { "transform": {}, "saveable": { "id": "Fox" }, "traveller": {} } }
        ] }
        """;

    private readonly ContentManagerTestsBuilder _files = new();
    private readonly string _saves = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public SceneTravelTestsBuilder()
    {
        _files.WithFile(Street, StreetJson).WithFile(Cellar, CellarJson);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public SceneTravel Travel { get; private set; } = null!;

    public SaveSystem Saves { get; private set; } = null!;

    public SceneTravelTestsBuilder Build()
    {
        ContentManager content = new(_files.RootDirectory);
        SceneLoader scenes = new(Game.Spawner, content, Game.World, NullLogger<SceneLoader>.Instance);
        Travel = Game.AddSceneTravel(scenes);
        Saves = Game.AddSaves(new SaveSettings { Folder = _saves });
        return this;
    }

    /// <summary>The living entity with the name, or <see cref="Entity.Null"/>.</summary>
    public Entity Find(string name)
    {
        Entity found = Entity.Null;
        QueryDescription named = new QueryDescription().WithAll<EntityName>();
        Game.World.Query(in named, (Entity entity, ref EntityName entityName) =>
        {
            if (entityName.Value == name)
            {
                found = entity;
            }
        });
        return found;
    }

    public ref Transform TransformOf(string name)
    {
        Entity entity = Find(name);
        return ref Game.World.Get<Transform>(entity);
    }

    public void Destroy(string name)
    {
        Entity entity = Find(name);
        Game.Lifecycle.Destroy(Game.World, entity);
    }

    public void Tick() => Game.Tick(Game.Loop.FixedStep);

    public void Dispose()
    {
        Game.Dispose();
        _files.Dispose();
        if (Directory.Exists(_saves))
        {
            Directory.Delete(_saves, recursive: true);
        }
    }
}
