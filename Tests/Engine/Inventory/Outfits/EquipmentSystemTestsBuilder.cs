using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Outfits;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Inventory.Tests.Outfits;

/// <summary>
/// Builds a playing game with saves, clothes, a bracelet and a backpack as item files, and a saveable hero who carries and wears what a
/// test gives.
/// </summary>
internal sealed class EquipmentSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly InventorySettings _settings = new() { EnforceWeight = false, EnforceSpace = false };
    private DataAssets? _data;
    private int _spawned;

    public EquipmentSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithItem("Coat", """{ "$type": "equippable", "slots": [ "Slot.Torso" ], "grantedTags": [ "Disguise.Police" ] }""", maxQuantity: 1);
        WithItem("Dress", """{ "$type": "equippable", "slots": [ "Slot.Torso" ], "blockedSlots": [ "Slot.Legs" ] }""");
        WithItem("Trousers", """{ "$type": "equippable", "slots": [ "Slot.Legs" ] }""");
        WithItem("Bracelet", """{ "$type": "equippable", "slots": [ "Slot.LeftArm", "Slot.RightArm" ] }""", maxQuantity: 1);
        WithItem("Backpack", """{ "$type": "equippable", "slots": [ "Slot.Back" ] }, { "$type": "container", "cells": 4 }""");
        WithItem("Apple", string.Empty, maxQuantity: 1);
        WithItem("Uniform", """
            { "$type": "equippable", "slots": [ "Slot.Torso" ], "equipConditions": { "conditions": [ { "$type": "fact", "fact": "Fact.Enlisted" } ] } }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public InventorySystem Inventories { get; private set; } = null!;

    public EquipmentSystem Equipment => Game.Services.GetService<EquipmentSystem>()!;

    public SaveSystem Saves { get; private set; } = null!;

    public Entity Hero { get; private set; }

    public static Gameplay.Tags.Tag Slot(string name) => new($"Slot.{name}");

    public ItemDefinition Item(string name) => _data!.Load<ItemDefinition>($"Items/{name}.fvdata");

    public EquipmentSystemTestsBuilder WithLimits(int pocketCells)
    {
        _settings.EnforceSpace = true;
        _settings.BasePocketCells = pocketCells;
        return this;
    }

    public EquipmentSystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Starts play with the hero carrying <paramref name="carried"/> (item name and quantity), its equipment given as scene JSON.</summary>
    public EquipmentSystemTestsBuilder Build(string carried, string equipment = "")
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Inventories = Game.AddInventory(_settings);
        string saves = Path.Combine(_folder, "Saves");
        Saves = Game.AddSaves(new SaveSettings { Folder = saves });
        Game.Lifecycle.Play(Game.World);
        Hero = Spawn(carried, equipment);
        return this;
    }

    public Entity Spawn(string carried, string equipment = "")
    {
        IEnumerable<string> entries = carried
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static entry => entry.Split(' '))
            .Select(static parts => $$"""{ "item": "Items/{{parts[0]}}.fvdata", "quantity": {{parts[1]}} }""");
        string items = string.Join(", ", entries);
        string json = $$"""
            { "entities": [ { "components": { "saveable": { "id": "Hero{{_spawned++}}" }, "inventory": { "items": [ {{items}} ] }, "equipment": { {{equipment}} } } } ] }
            """;
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

    private void WithItem(string name, string fragments, int maxQuantity = 0)
    {
        WithFile($"Items/{name}.fvdata", $$"""{ "type": "item", "maxQuantity": {{maxQuantity}}, "fragments": [ {{fragments}} ] }""");
    }
}
