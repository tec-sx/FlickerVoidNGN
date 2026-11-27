using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Props.Serialization;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Props.Tests;

/// <summary>Builds a playing game with rendering, physics, interaction and props over a temporary content folder, without a window.</summary>
internal sealed class PropEntityTestsBuilder : IDisposable
{
    public const string File = "Props/Box.fvprop";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidPropTests", Guid.NewGuid().ToString("N"));

    public PropEntityTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
        Game.Services.AddService<IContentManager>(Content);
        Game.AddRendering();
        Physics = Game.AddPhysics();
        Game.AddInteraction();
        Game.AddProps();
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public World World => Game.World;

    public ContentManager Content { get; }

    public IPhysicsSystem Physics { get; }

    public CreationParts Parts => Game.Services.GetService<CreationParts>()!;

    /// <summary>Saves the file anew, as the editor does, which reports it changed.</summary>
    public void Write(PropFile prop)
    {
        string json = JsonSerializer.Serialize(prop, PropsJsonContext.Default.PropFile);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Content.Write(File, bytes);
    }

    /// <summary>Spawns a playing scene entity with a prop naming the file, and lets a step pass.</summary>
    public Entity Place()
    {
        string json = $$"""{ "entities": [ { "name": "Box", "components": { "transform": {}, "prop": { "file": "{{File}}" } } } ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        Entity prop = Game.Spawner.Spawn(scene, World)[0];
        Game.Lifecycle.Play(World);
        Game.Tick(Game.Loop.FixedStep);
        return prop;
    }

    public void Dispose()
    {
        Game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
