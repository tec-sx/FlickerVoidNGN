using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Ai;
using FlickerVoid.Engine.Ai.Paths;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Buildings;

/// <summary>Builds a playing game with physics, characters, paths and buildings around one building written to a temporary content folder, standing on a wide ground.</summary>
internal sealed class BuildingWalkTestsBuilder : IDisposable
{
    private const string File = "Buildings/House.fvbuilding";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidBuildingWalkTests", Guid.NewGuid().ToString("N"));
    private readonly ContentManager _content;

    public BuildingWalkTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        _content = new ContentManager(_folder);
        Game.Services.AddService<IContentManager>(_content);
        Game.AddRendering();
        Game.AddPhysics();
        Game.AddCharacters();
        Paths = Game.AddAi();
        Game.AddInteraction();
        Game.AddBuildings();
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public PathSystem Paths { get; }

    /// <summary>Places the building at the origin of a playing level and lets its parts spawn.</summary>
    public void Place(BuildingFile building)
    {
        string json = JsonSerializer.Serialize(building, BuildingsJsonContext.Default.BuildingFile);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        _content.Write(File, bytes);
        Spawn("""{ "name": "Ground", "components": { "transform": { "position": [0, -0.1, 0] }, "staticBody": { "collider": { "type": "box", "size": [30, 0.2, 30] } } } }""");
        Spawn($$"""{ "name": "House", "components": { "transform": {}, "building": { "file": "{{File}}" } } }""");
        Game.Lifecycle.Play(Game.World);
        Run(0.2f);
    }

    public Entity AddWalker(Vector3 position)
    {
        Entity walker = Spawn($$"""{ "name": "Hero", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "characterMover": { "speed": 2 } } }""");
        Game.Lifecycle.Start(Game.World, walker);
        return walker;
    }

    public Vector3 PlaceOf(Entity entity) => Game.World.Get<Transform>(entity).Position;

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
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }
}
