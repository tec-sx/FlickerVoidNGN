using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Inventory.Tests.Rules;

/// <summary>Builds a playing game with inventories and an item file, and contexts with a hero instigating and a merchant as target.</summary>
internal sealed class InventoryRulesTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public InventoryRulesTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Items/Coin.fvdata", """{ "type": "item", "tags": [ "Item.Coin" ] }""");
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InventorySystem Inventories { get; private set; } = null!;

    public Entity Hero { get; private set; }

    public Entity Merchant { get; private set; }

    public InventoryRulesTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Starts play with a hero and a merchant, their inventories given as scene JSON.</summary>
    public InventoryRulesTestsBuilder Build(string hero = "", string merchant = "")
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Inventories = Game.AddInventory(new InventorySettings { EnforceWeight = false, EnforceSpace = false });
        Game.Lifecycle.Play(Game.World);
        string json = $$"""
            { "entities": [
                { "components": { "inventory": { {{hero}} } } },
                { "components": { "inventory": { {{merchant}} } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Merchant = spawned[1];
        return this;
    }

    public ConditionContext Context()
    {
        WorldEntityView hero = new(Game.World, Hero);
        WorldEntityView merchant = new(Game.World, Merchant);
        return new ConditionContext(Game.Facts) { Instigator = hero, Target = merchant, Services = Game.Services };
    }

    public ItemDefinition Coin => _data!.Load<ItemDefinition>("Items/Coin.fvdata");

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
