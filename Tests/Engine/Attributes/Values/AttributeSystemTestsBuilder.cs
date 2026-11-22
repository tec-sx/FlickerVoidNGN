using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Attributes.Tests.Values;

/// <summary>
/// Builds a playing game with saves and attributes over Stealth (0 to 10, whole), Health (0 to 100) and a Drunk modifier, with a
/// saveable hero and a guard.
/// </summary>
internal sealed class AttributeSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public AttributeSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Attributes/Stealth.fvdata", """
            { "type": "attribute", "id": "Attribute.Skill.Stealth", "display": { "name": "Stealth" }, "category": "Attribute.Skill", "min": 0, "max": 10, "integer": true }
            """);
        WithFile("Attributes/Health.fvdata", """
            { "type": "attribute", "id": "Attribute.Physical.Health", "display": { "name": "Health" }, "max": 100 }
            """);
        WithFile("Modifiers/Drunk.fvdata", """
            { "type": "modifier", "display": { "name": "Drunk" }, "grantedTags": [ "Status.Drunk" ], "changes": [
                { "attribute": "Attribute.Skill.Stealth", "operation": "Add", "value": -2 },
                { "attribute": "Attribute.Physical.Health", "operation": "Multiply", "value": 0.5 } ] }
            """);
    }

    public const string Drunk = "Modifiers/Drunk.fvdata";

    public static readonly Gameplay.Tags.Tag Stealth = new("Attribute.Skill.Stealth");

    public static readonly Gameplay.Tags.Tag Health = new("Attribute.Physical.Health");

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public AttributeSystem Attributes { get; private set; } = null!;

    public SaveSystem Saves { get; private set; } = null!;

    public Entity Hero { get; private set; }

    public Entity Guard { get; private set; }

    public AttributeSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Starts play with a hero and a guard, the values of their <c>attributes</c> components given as JSON.</summary>
    public AttributeSystemTestsBuilder Build(string hero = "", string guard = "")
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Attributes = Game.AddAttributes();
        string saves = Path.Combine(_folder, "Saves");
        Saves = Game.AddSaves(new SaveSettings { Folder = saves });
        Game.Lifecycle.Play(Game.World);
        string json = $$"""
            { "entities": [
                { "components": { "saveable": { "id": "Hero" }, "attributes": { "values": [ {{hero}} ] } } },
                { "components": { "attributes": { "values": [ {{guard}} ] } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Guard = spawned[1];
        return this;
    }

    public ConditionContext Context()
    {
        WorldEntityView hero = new(Game.World, Hero);
        WorldEntityView guard = new(Game.World, Guard);
        return new ConditionContext(Game.Facts) { Instigator = hero, Target = guard, Services = Game.Services };
    }

    public T Load<T>(string path)
        where T : class
    {
        return _data!.Load<T>(path);
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispatch() => Game.Events.Dispatch();

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
