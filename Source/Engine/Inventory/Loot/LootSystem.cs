using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Inventory.Loot;

/// <summary>Rolls loot tables and their nested tables, on their own or into an inventory, and fills lootable containers once.</summary>
public sealed partial class LootSystem
{
    private const int MaxDepth = 8;

    private readonly World _world;
    private readonly IFactDatabase _facts;
    private readonly IServiceRegistry _services;
    private readonly InventorySystem _inventories;
    private readonly Random _random;
    private readonly ILogger<LootSystem> _logger;

    public LootSystem(Game game, InventorySystem inventories, Random random)
    {
        _world = game.World;
        _facts = game.Facts;
        _services = game.Services;
        _inventories = inventories;
        _random = random;
        _logger = game.LoggerFactory.CreateLogger<LootSystem>();
    }

    /// <summary>Rolls the table at <paramref name="table"/> and the tables it nests; entry conditions see the instigator and the target.</summary>
    public LootResult Roll(string table, Entity instigator, Entity target)
    {
        Roller roller = new(this, CreateContext(instigator, target));
        roller.RollTable(table, 0);
        if (roller.TooDeep)
        {
            LogTooDeep(table, MaxDepth);
        }

        if (roller.Cycle)
        {
            LogCycle(table);
        }

        return new LootResult(roller.Stacks, roller.Money);
    }

    /// <summary>Rolls the table with the receiver as target and adds the items, as far as they fit, and the money; the units added.</summary>
    public int GiveLoot(string table, Entity receiver, Entity instigator)
    {
        if (!_inventories.HasInventory(receiver))
        {
            return 0;
        }

        LootResult loot = Roll(table, instigator, receiver);
        int added = 0;
        foreach (ItemStack stack in loot.Stacks)
        {
            added += _inventories.AddItem(receiver, stack.Item, stack.Quantity);
        }

        _inventories.AddMoney(receiver, loot.Money);
        return added;
    }

    /// <summary>Fills a lootable entity's inventory from its table unless it was rolled before; true when it rolled now.</summary>
    public bool RollIfNeeded(Entity lootable, Entity instigator)
    {
        if (!_world.IsAlive(lootable) || !_world.Has<Lootable>(lootable))
        {
            return false;
        }

        ref Lootable loot = ref _world.Get<Lootable>(lootable);
        if (loot.Rolled || loot.Table.Length == 0)
        {
            return false;
        }

        if (!_inventories.HasInventory(lootable))
        {
            LogNoInventory(loot.Table);
            return false;
        }

        loot.Rolled = true;
        string table = loot.Table;
        GiveLoot(table, lootable, instigator);
        return true;
    }

    private ConditionContext CreateContext(Entity instigator, Entity target)
    {
        WorldEntityView instigatorView = new(_world, instigator);
        WorldEntityView targetView = new(_world, target);
        return new ConditionContext(_facts) { Instigator = instigatorView, Target = targetView, Services = _services };
    }

    private T? Load<T>(string path)
        where T : class
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null || path.Length == 0)
        {
            return null;
        }

        try
        {
            return data.Load<T>(path);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            LogLoadFailed(path, exception.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The loot table {Table} nests more than {Depth} deep; the deeper rolls were skipped.")]
    private partial void LogTooDeep(string table, int depth);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A table nested in the loot table {Table} leads back to a table already being rolled; that roll was skipped.")]
    private partial void LogCycle(string table);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The lootable with the table {Table} has no inventory to fill.")]
    private partial void LogNoInventory(string table);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The loot file {Path} can't be read, so it gives nothing: {Reason}")]
    private partial void LogLoadFailed(string path, string reason);

    /// <summary>One roll of a table: what it gave so far and the tables it is inside, to stop cycles.</summary>
    private sealed class Roller(LootSystem system, ConditionContext context)
    {
        private readonly List<string> _path = [];

        public List<ItemStack> Stacks { get; } = [];

        public int Money { get; private set; }

        public bool TooDeep { get; private set; }

        public bool Cycle { get; private set; }

        public void RollTable(string path, int depth)
        {
            if (depth >= MaxDepth)
            {
                TooDeep = true;
                return;
            }

            if (_path.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                Cycle = true;
                return;
            }

            LootTableDefinition? table = system.Load<LootTableDefinition>(path);
            if (table is null)
            {
                return;
            }

            _path.Add(path);
            int minMoney = Math.Max(0, table.MinMoney);
            int maxMoney = Math.Max(minMoney, table.MaxMoney);
            Money += system._random.Next(minMoney, maxMoney + 1);
            foreach (LootEntry entry in table.Entries)
            {
                RollEntry(entry, depth);
            }

            _path.RemoveAt(_path.Count - 1);
        }

        private void RollEntry(LootEntry entry, int depth)
        {
            bool empty = entry.Item.Length == 0 && entry.Table.Length == 0;

            // A sure entry skips the roll.
            bool missed = entry.Chance < 1 && system._random.NextSingle() >= entry.Chance;
            if (empty || missed || !entry.Conditions.Evaluate(context))
            {
                return;
            }

            int minQuantity = Math.Max(1, entry.MinQuantity);
            int maxQuantity = Math.Max(minQuantity, entry.MaxQuantity);
            int quantity = system._random.Next(minQuantity, maxQuantity + 1);
            if (entry.Table.Length > 0)
            {
                for (int index = 0; index < quantity; index++)
                {
                    RollTable(entry.Table, depth + 1);
                }

                return;
            }

            ItemDefinition? item = system.Load<ItemDefinition>(entry.Item);
            if (item is not null)
            {
                AddUnits(item, quantity);
            }
        }

        private void AddUnits(ItemDefinition item, int quantity)
        {
            ItemStack? existing = Stacks.Find(stack => stack.Item == item);
            if (existing is not null)
            {
                existing.Quantity += quantity;
                return;
            }

            Stacks.Add(new ItemStack(item, quantity));
        }
    }
}
