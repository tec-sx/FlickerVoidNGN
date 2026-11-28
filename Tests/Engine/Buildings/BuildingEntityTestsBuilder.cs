using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Buildings.Tests;

/// <summary>Builds a playing game with rendering, physics, characters, interaction and buildings over a temporary content folder, without a window.</summary>
internal sealed class BuildingEntityTestsBuilder : IDisposable
{
    public const string File = "Buildings/Inn.fvbuilding";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidBuildingTests", Guid.NewGuid().ToString("N"));

    public BuildingEntityTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
        Game.Services.AddService<IContentManager>(Content);
        Game.AddRendering();
        Physics = Game.AddPhysics();
        Game.AddCharacters();
        Game.AddInteraction();
        Director = Game.AddCameras();
        Game.AddBuildings();
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public World World => Game.World;

    public ContentManager Content { get; }

    public IPhysicsSystem Physics { get; }

    public CreationParts Parts => Game.Services.GetService<CreationParts>()!;

    public CameraDirector Director { get; }

    /// <summary>Saves the file anew, as the editor does, which reports it changed.</summary>
    public void Write(BuildingFile building)
    {
        string json = JsonSerializer.Serialize(building, BuildingsJsonContext.Default.BuildingFile);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Content.Write(File, bytes);
    }

    /// <summary>Spawns a camera shot of the level standing at the point, the one the scene starts on.</summary>
    public Entity AddStreetShot()
    {
        return Spawn("""{ "name": "Street", "components": { "transform": { "position": [0, 10, -20] }, "camera": {}, "shot": { "start": true } } }""");
    }

    /// <summary>Moves an entity at once, as a teleport.</summary>
    public void Teleport(Entity entity, Vector3 position)
    {
        Transform transform = World.Get<Transform>(entity);
        transform.Position = position;
        World.Set(entity, transform);
    }

    /// <summary>Spawns a playing scene entity named Inn with a building naming the file, and lets a step pass.</summary>
    public Entity Place()
    {
        Entity building = Spawn($$"""{ "name": "Inn", "components": { "transform": {}, "building": { "file": "{{File}}" } } }""");
        Game.Lifecycle.Play(World);
        Run(0.1f);
        return building;
    }

    public Entity AddWalker(Vector3 position)
    {
        Entity walker = Spawn($$"""{ "name": "Hero", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "characterMover": {}, "triggerActivator": {}, "cameraTarget": {} } }""");
        Game.Lifecycle.Start(World, walker);
        return walker;
    }

    public Transform PlaceOf(Entity entity) => World.Get<Transform>(entity);

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
        Directory.Delete(_folder, recursive: true);
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, World)[0];
    }
}
