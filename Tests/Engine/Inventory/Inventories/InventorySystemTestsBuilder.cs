using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Inventory.Tests.Inventories;

/// <summary>Builds a playing game with inventories over data files in a temporary content folder, with the limits a test sets.</summary>
internal sealed class InventorySystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly InventorySettings _settings = new() { EnforceWeight = false, EnforceSpace = false };
    private DataAssets? _data;

    public InventorySystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InventorySystem Inventories { get; private set; } = null!;

    public static ItemDefinition Item(float weight = 1, int maxQuantity = 0, int width = 1, int height = 1)
    {
        return new ItemDefinition { Weight = weight, MaxQuantity = maxQuantity, Width = width, Height = height };
    }

    /// <summary>An item whose use adds one to <paramref name="fact"/>, guarded by <paramref name="conditions"/>.</summary>
    public static ItemDefinition Usable(string fact, int charges = 0, bool consumeOnUse = true, params Condition[] conditions)
    {
        ItemDefinition item = Item();
        FactEffect effect = new() { Fact = new(fact), Write = FactWrite.Add };
        UsableFragment usable = new() { ConsumeOnUse = consumeOnUse };
        usable.Effects.Effects.Add(effect);
        usable.UseConditions.Conditions.AddRange(conditions);
        item.Fragments.Add(usable);
        if (charges > 0)
        {
            item.Fragments.Add(new ChargesFragment { Charges = charges });
        }

        return item;
    }

    public InventorySystemTestsBuilder WithLimits(bool weight, bool space, float weightLimit, int pocketCells)
    {
        _settings.EnforceWeight = weight;
        _settings.EnforceSpace = space;
        _settings.BaseWeightLimit = weightLimit;
        _settings.BasePocketCells = pocketCells;
        return this;
    }

    public InventorySystemTestsBuilder WithKeyCategory(string category)
    {
        _settings.KeyItemCategories.Add(new(category));
        return this;
    }

    public InventorySystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Adds inventories and the data assets service, then starts play, so spawned entities activate.</summary>
    public InventorySystemTestsBuilder Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Inventories = Game.AddInventory(_settings);
        Game.Lifecycle.Play(Game.World);
        return this;
    }

    /// <summary>Spawns an entity with an inventory, its values given as scene JSON.</summary>
    public Entity Owner(string inventory = "")
    {
        string json = $$"""{ "entities": [ { "components": { "inventory": { {{inventory}} } } } ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        return spawned[0];
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
