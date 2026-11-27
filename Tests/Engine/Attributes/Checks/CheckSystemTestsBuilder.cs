using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Attributes.Checks;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Attributes.Tests.Checks;

/// <summary>Builds a playing game with attributes and seeded checks over a Stealth attribute, with a hero of the given Stealth.</summary>
internal sealed class CheckSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public CheckSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Attributes/Stealth.fvdata", """
            { "type": "attribute", "id": "Attribute.Skill.Stealth", "display": { "name": { "key": "Test.Stealth", "text": "Stealth" } }, "max": 10, "integer": true }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public CheckSystem Checks { get; private set; } = null!;

    public Entity Hero { get; private set; }

    public CheckSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public CheckSystemTestsBuilder Build(int stealth = 0)
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Game.AddAttributes(new Random(7));
        Checks = Game.Services.GetService<CheckSystem>()!;
        Game.Lifecycle.Play(Game.World);
        string json = $$"""
            { "entities": [ { "components": { "attributes": { "values": [ { "attribute": "Attribute.Skill.Stealth", "value": {{stealth}} } ] } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        Hero = Game.Spawner.Spawn(scene, Game.World)[0];
        return this;
    }

    public ConditionContext Context()
    {
        WorldEntityView hero = new(Game.World, Hero);
        return new ConditionContext(Game.Facts) { Instigator = hero, Services = Game.Services };
    }

    public CheckDefinition Load(string path) => _data!.Load<CheckDefinition>(path);

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
