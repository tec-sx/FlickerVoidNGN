using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Pickups;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Inventory.Tests.Pickups;

/// <summary>Builds a playing game with a hero who carries at most the weight a test sets and a pickup of apples or of a usable bottle.</summary>
internal sealed class PickupSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly InventorySettings _settings = new() { EnforceSpace = false, BaseWeightLimit = 100 };
    private DataAssets? _data;

    public PickupSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Items/Apple.fvdata", """{ "type": "item", "weight": 1 }""");
        WithFile("Items/Bottle.fvdata", """
            {
              "type": "item",
              "fragments": [
                { "$type": "usable", "effects": { "effects": [ { "$type": "fact", "fact": "Fact.Sips", "write": "Add" } ] } },
                { "$type": "charges", "charges": 2 }
              ]
            }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InventorySystem Inventories { get; private set; } = null!;

    public PickupSystem Pickups => Game.Services.GetService<PickupSystem>()!;

    public Entity Hero { get; private set; }

    public Entity Pickup { get; private set; }

    public PickupSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    public PickupSystemTestsBuilder WithWeightLimit(float limit)
    {
        _settings.BaseWeightLimit = limit;
        return this;
    }

    /// <summary>Starts play with the hero and the pickup, its values given as scene JSON.</summary>
    public PickupSystemTestsBuilder Build(string pickup)
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Inventories = Game.AddInventory(_settings);
        Game.Lifecycle.Play(Game.World);
        string json = $$"""
            { "entities": [
                { "components": { "inventory": {} } },
                { "components": { "pickup": { {{pickup}} } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Pickup = spawned[1];
        return this;
    }

    /// <summary>The context an interaction offer applies its effects with: the hero instigating, the pickup as target.</summary>
    public ConditionContext Context()
    {
        WorldEntityView hero = new(Game.World, Hero);
        WorldEntityView pickup = new(Game.World, Pickup);
        return new ConditionContext(Game.Facts) { Instigator = hero, Target = pickup, Services = Game.Services };
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public int Left() => Game.World.Get<ActivePickup>(Pickup).Stack.Quantity;

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
