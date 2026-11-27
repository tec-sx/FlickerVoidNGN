using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Foliage.Serialization;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Foliage.Tests;

/// <summary>Builds a playing game with rendering, physics and foliage over a temporary content folder, without a window.</summary>
internal sealed class FoliageEntityTestsBuilder : IDisposable
{
    public const string File = "Foliage/Oak.fvfoliage";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidFoliageTests", Guid.NewGuid().ToString("N"));

    public FoliageEntityTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
        Game.Services.AddService<IContentManager>(Content);
        Game.AddRendering();
        Physics = Game.AddPhysics();
        Game.AddFoliage();
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public World World => Game.World;

    public ContentManager Content { get; }

    public IPhysicsSystem Physics { get; }

    public CreationParts Parts => Game.Services.GetService<CreationParts>()!;

    /// <summary>Saves the file anew, as the editor does, which reports it changed.</summary>
    public void Write(FoliageFile foliage)
    {
        string json = JsonSerializer.Serialize(foliage, FoliageJsonContext.Default.FoliageFile);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Content.Write(File, bytes);
    }

    /// <summary>Spawns a playing scene entity with the component of that name given as JSON, and lets a step pass.</summary>
    public Entity Place(string component, string json)
    {
        string sceneJson = $$"""{ "entities": [ { "name": "Oak", "components": { "transform": {}, "{{component}}": {{json}} } } ] }""";
        SceneData scene = JsonSerializer.Deserialize(sceneJson, RuntimeJsonContext.Default.SceneData)!;
        Entity entity = Game.Spawner.Spawn(scene, World)[0];
        Game.Lifecycle.Play(World);
        Game.Tick(Game.Loop.FixedStep);
        return entity;
    }

    public Entity PlacePlant() => Place("foliage", $$"""{ "file": "{{File}}" }""");

    public void Dispose()
    {
        Game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
