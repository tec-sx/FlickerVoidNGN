using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Loot;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Tests.Loot;

public sealed class LootSystemTests : IDisposable
{
    private readonly LootSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SureEntriesAreGivenAndMissedOnesAreNot()
    {
        _builder.WithTable("Purse", """
            { "item": "Items/Coin.fvdata", "minQuantity": 3, "maxQuantity": 3 },
            { "item": "Items/Ring.fvdata", "chance": 0 }
            """, minMoney: 5, maxMoney: 5).Build();

        LootResult loot = _builder.Loot.Roll("Loot/Purse.fvdata", _builder.Hero, _builder.Chest);

        loot.Stacks.Should().ContainSingle().Which.Quantity.Should().Be(3);
        loot.Stacks[0].Item.Should().BeSameAs(_builder.Item("Coin"));
        loot.Money.Should().Be(5);
    }

    [Fact]
    public void QuantitiesAndMoneyStayInTheirRanges()
    {
        _builder.WithTable("Purse", """{ "item": "Items/Coin.fvdata", "minQuantity": 2, "maxQuantity": 4 }""", minMoney: 1, maxMoney: 3).Build();

        List<LootResult> rolls = Enumerable.Range(0, 50).Select(_ => _builder.Loot.Roll("Loot/Purse.fvdata", _builder.Hero, _builder.Chest)).ToList();

        rolls.Select(static loot => loot.Stacks[0].Quantity).Should().OnlyContain(quantity => quantity >= 2 && quantity <= 4).And.Contain([2, 3, 4]);
        rolls.Select(static loot => loot.Money).Should().OnlyContain(money => money >= 1 && money <= 3).And.Contain([1, 3]);
    }

    [Fact]
    public void NestedTablesAreRolledTheirQuantityOfTimesAndMerged()
    {
        _builder.WithTable("Coins", """{ "item": "Items/Coin.fvdata", "minQuantity": 2, "maxQuantity": 2 }""", minMoney: 1, maxMoney: 1)
            .WithTable("Chest", """
                { "table": "Loot/Coins.fvdata", "minQuantity": 3, "maxQuantity": 3 },
                { "item": "Items/Coin.fvdata" }
                """).Build();

        LootResult loot = _builder.Loot.Roll("Loot/Chest.fvdata", _builder.Hero, _builder.Chest);

        loot.Stacks.Should().ContainSingle().Which.Quantity.Should().Be(7);
        loot.Money.Should().Be(3);
    }

    [Fact]
    public void ATableLeadingBackToItselfIsSkipped()
    {
        _builder.WithTable("Loop", """{ "table": "Loot/Loop.fvdata" }, { "item": "Items/Coin.fvdata" }""").Build();

        LootResult loot = _builder.Loot.Roll("Loot/Loop.fvdata", _builder.Hero, _builder.Chest);

        loot.Stacks.Should().ContainSingle().Which.Quantity.Should().Be(1);
    }

    [Fact]
    public void EntryConditionsSeeTheLooter()
    {
        _builder.WithTable("Desk", """
            { "item": "Items/Ring.fvdata", "conditions": { "conditions": [ { "$type": "hasItem", "item": "Items/Coin.fvdata" } ] } }
            """).Build();

        LootResult poor = _builder.Loot.Roll("Loot/Desk.fvdata", _builder.Hero, _builder.Chest);
        _builder.Inventories.AddItem(_builder.Hero, _builder.Item("Coin"));
        LootResult rich = _builder.Loot.Roll("Loot/Desk.fvdata", _builder.Hero, _builder.Chest);

        poor.Stacks.Should().BeEmpty();
        rich.Stacks.Should().ContainSingle();
    }

    [Fact]
    public void GivenLootFillsTheReceiverAsFarAsItFits()
    {
        _builder.WithTable("Rings", """{ "item": "Items/Ring.fvdata", "minQuantity": 5, "maxQuantity": 5 }""", minMoney: 2, maxMoney: 2)
            .WithWeightLimit(3).Build();

        int added = _builder.Loot.GiveLoot("Loot/Rings.fvdata", _builder.Hero, _builder.Hero);

        added.Should().Be(3);
        _builder.Inventories.GetMoney(_builder.Hero).Should().Be(2);
    }

    [Fact]
    public void ALootableFillsOnlyOnce()
    {
        _builder.WithTable("Chest", """{ "item": "Items/Ring.fvdata" }""").Build("\"table\": \"Loot/Chest.fvdata\", \"owner\": \"Faction.Merchants\"");

        bool first = _builder.Loot.RollIfNeeded(_builder.Chest, _builder.Hero);
        bool second = _builder.Loot.RollIfNeeded(_builder.Chest, _builder.Hero);

        first.Should().BeTrue();
        second.Should().BeFalse();
        _builder.Inventories.GetQuantity(_builder.Chest, _builder.Item("Ring")).Should().Be(1);
        _builder.Game.World.Get<Lootable>(_builder.Chest).Owner.Should().Be(new Tag("Faction.Merchants"));
    }

    [Fact]
    public void TheGiveLootEffectRollsIntoItsSubject()
    {
        _builder.WithTable("Reward", """{ "item": "Items/Coin.fvdata" }""").Build();
        WorldEntityView hero = new(_builder.Game.World, _builder.Hero);
        WorldEntityView chest = new(_builder.Game.World, _builder.Chest);
        ConditionContext context = new(_builder.Game.Facts) { Instigator = hero, Target = chest, Services = _builder.Game.Services };

        new GiveLootEffect { Table = "Loot/Reward.fvdata", Subject = ContextEntity.Target }.Apply(context);

        _builder.Inventories.GetQuantity(_builder.Chest, _builder.Item("Coin")).Should().Be(1);
    }
}
