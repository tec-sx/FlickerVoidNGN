using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Loot;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Inventory.Tests.Loot;

/// <summary>Builds a playing game with loot over item and table files, rolling with a seeded random, and a hero and a chest.</summary>
internal sealed class LootSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly InventorySettings _settings = new() { EnforceWeight = false, EnforceSpace = false };
    private DataAssets? _data;

    public LootSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Items/Coin.fvdata", """{ "type": "item" }""");
        WithFile("Items/Ring.fvdata", """{ "type": "item", "weight": 1 }""");
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InventorySystem Inventories { get; private set; } = null!;

    public LootSystem Loot => Game.Services.GetService<LootSystem>()!;

    public Entity Hero { get; private set; }

    public Entity Chest { get; private set; }

    public ItemDefinition Item(string name) => _data!.Load<ItemDefinition>($"Items/{name}.fvdata");

    public LootSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Writes a loot table of the given entries and money range.</summary>
    public LootSystemTestsBuilder WithTable(string name, string entries, int minMoney = 0, int maxMoney = 0)
    {
        return WithFile($"Loot/{name}.fvdata", $$"""{ "type": "lootTable", "entries": [ {{entries}} ], "minMoney": {{minMoney}}, "maxMoney": {{maxMoney}} }""");
    }

    public LootSystemTestsBuilder WithWeightLimit(float limit)
    {
        _settings.EnforceWeight = true;
        _settings.BaseWeightLimit = limit;
        return this;
    }

    /// <summary>Starts play with the hero and a chest, the chest's lootable given as scene JSON.</summary>
    public LootSystemTestsBuilder Build(string lootable = "")
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Inventories = Game.AddInventory(_settings, new Random(7));
        Game.Lifecycle.Play(Game.World);
        string json = $$"""
            { "entities": [
                { "components": { "inventory": {} } },
                { "components": { "inventory": { "applyCarryLimits": false }, "lootable": { {{lootable}} } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Chest = spawned[1];
        return this;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
