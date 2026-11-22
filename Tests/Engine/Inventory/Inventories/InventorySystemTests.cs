using Arch.Core;

using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;

namespace FlickerVoid.Engine.Inventory.Tests.Inventories;

public sealed class InventorySystemTests : IDisposable
{
    private readonly InventorySystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AddsOnlyTheUnitsThatFitTheWeightLimit()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: false, weightLimit: 10, pocketCells: 0).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(weight: 3);

        int added = inventories.AddItem(owner, item, 5);

        added.Should().Be(3);
        inventories.GetQuantity(owner, item).Should().Be(3);
        inventories.GetTotalWeight(owner).Should().Be(9);
        inventories.CanAddItem(owner, item).Should().BeFalse();
    }

    [Fact]
    public void CountsTheBonusTheGameGrants()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: false, weightLimit: 10, pocketCells: 0).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(weight: 3);

        inventories.SetBonuses(owner, 5, 0);
        int added = inventories.AddItem(owner, item, 10);

        added.Should().Be(5, "15 kg holds five units of 3 kg");
    }

    [Fact]
    public void StoryItemsIgnoreTheLimits()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: true, weightLimit: 10, pocketCells: 0).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(weight: 3);

        int added = inventories.AddStoryItem(owner, item, 5);

        added.Should().Be(5);
        inventories.GetQuantity(owner, item).Should().Be(5);
    }

    [Fact]
    public void GivesEveryNewStackItsOwnCells()
    {
        InventorySystem inventories = _builder.WithLimits(weight: false, space: true, weightLimit: 0, pocketCells: 2).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(maxQuantity: 1);

        int added = inventories.AddItem(owner, item, 5);

        added.Should().Be(2);
        inventories.GetUsedCells(owner).Should().Be(2);
        inventories.GetStacks(owner).Should().HaveCount(2);
    }

    [Fact]
    public void FillsAnUnlimitedStackForFree()
    {
        InventorySystem inventories = _builder.WithLimits(weight: false, space: true, weightLimit: 0, pocketCells: 1).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item();

        int first = inventories.AddItem(owner, item, 100);
        int second = inventories.AddItem(owner, item, 50);

        first.Should().Be(100);
        second.Should().Be(50);
        inventories.GetUsedCells(owner).Should().Be(1);
    }

    [Fact]
    public void FillsExistingStacksBeforeClaimingNewCells()
    {
        InventorySystem inventories = _builder.WithLimits(weight: false, space: true, weightLimit: 0, pocketCells: 2).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(maxQuantity: 10);

        int first = inventories.AddItem(owner, item, 4);
        int second = inventories.AddItem(owner, item, 20);

        first.Should().Be(4);
        second.Should().Be(16, "two stacks of 10 fit");
        inventories.GetQuantity(owner, item).Should().Be(20);
    }

    [Fact]
    public void RefusesAnItemLargerThanTheFreeSpace()
    {
        InventorySystem inventories = _builder.WithLimits(weight: false, space: true, weightLimit: 0, pocketCells: 2).Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(maxQuantity: 1, width: 2, height: 2);

        int added = inventories.AddItem(owner, item, 1);

        added.Should().Be(0);
        inventories.GetStacks(owner).Should().BeEmpty();
    }

    [Fact]
    public void KeyItemsTakeNoWeightOrCellsAndAreListedApart()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: true, weightLimit: 0, pocketCells: 0).WithKeyCategory("Item.Key").Build().Inventories;
        Entity owner = _builder.Owner("\"applyCarryLimits\": true");
        ItemDefinition key = InventorySystemTestsBuilder.Item(weight: 2);
        key.Category = new Tag("Item.Key.Door");

        int added = inventories.AddItem(owner, key, 1);

        added.Should().Be(1);
        inventories.GetTotalWeight(owner).Should().Be(0);
        inventories.GetUsedCells(owner).Should().Be(0);
        inventories.GetKeyItems(owner).Should().ContainSingle().Which.Item.Should().BeSameAs(key);
        inventories.GetStorageItems(owner).Should().BeEmpty();
    }

    [Fact]
    public void InventoriesWithoutCarryLimitsTakeAnyAmount()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: true, weightLimit: 0, pocketCells: 0).Build().Inventories;
        Entity chest = _builder.Owner("\"applyCarryLimits\": false");
        ItemDefinition item = InventorySystemTestsBuilder.Item(weight: 5, maxQuantity: 1);

        int added = inventories.AddItem(chest, item, 3);

        added.Should().Be(3);
    }

    [Fact]
    public void FindsItemsByTheirTags()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition key = InventorySystemTestsBuilder.Item();
        key.Tags.Add(new Tag("Key.Cellar"));
        inventories.AddItem(owner, key, 2);

        ItemStack? found = inventories.FindItemWithTag(owner, new Tag("Key"));

        found.Should().NotBeNull();
        found!.Item.Should().BeSameAs(key);
        inventories.GetQuantityWithTag(owner, new Tag("Key.Cellar")).Should().Be(2);
        inventories.FindItemWithTag(owner, new Tag("Key.Attic")).Should().BeNull();
    }

    [Fact]
    public void RemovesNothingWhenThereIsNotEnough()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item();
        inventories.AddItem(owner, item, 3);

        bool tooMany = inventories.RemoveItem(owner, item, 4);
        int kept = inventories.GetQuantity(owner, item);
        bool some = inventories.RemoveItem(owner, item, 2);

        tooMany.Should().BeFalse();
        kept.Should().Be(3);
        some.Should().BeTrue();
        inventories.GetQuantity(owner, item).Should().Be(1);
    }

    [Fact]
    public void RemovesPlainUnitsBeforeCustomizedOnes()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item();
        ItemStack stolen = new(item, 1);
        stolen.SetData(new Stolen(new Tag("Faction.Police")));
        Guid stolenId = inventories.AddStack(owner, stolen);
        inventories.AddItem(owner, item, 2);

        inventories.RemoveItem(owner, item, 2);

        inventories.GetStacks(owner).Should().ContainSingle().Which.Id.Should().Be(stolenId);
    }

    [Fact]
    public void SpendsMoneyOnlyWhenThereIsEnough()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        inventories.AddMoney(owner, 10);

        bool tooMuch = inventories.RemoveMoney(owner, 11);
        bool enough = inventories.RemoveMoney(owner, 4);

        tooMuch.Should().BeFalse();
        enough.Should().BeTrue();
        inventories.GetMoney(owner).Should().Be(6);
    }

    [Fact]
    public void UsingAnItemAppliesItsEffectsToItsOwnerAndConsumesAUnit()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition bread = InventorySystemTestsBuilder.Usable("Fact.Eaten");
        inventories.AddItem(owner, bread, 2);
        List<ItemUsedEvent> used = _builder.Collect<ItemUsedEvent>();

        bool done = inventories.UseItem(owner, bread);
        _builder.Game.Events.Dispatch();

        done.Should().BeTrue();
        _builder.Game.Facts.GetFact(new Tag("Fact.Eaten")).Should().Be(1);
        inventories.GetQuantity(owner, bread).Should().Be(1);
        used.Should().Equal(new ItemUsedEvent(owner, bread));
    }

    [Fact]
    public void UseConditionsGuardUse()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        FactCondition awake = new() { Fact = new Tag("Fact.Awake") };
        ItemDefinition coffee = InventorySystemTestsBuilder.Usable("Fact.Drunk", conditions: awake);
        inventories.AddItem(owner, coffee, 1);

        bool asleep = inventories.UseItem(owner, coffee);
        _builder.Game.Facts.SetFact(new Tag("Fact.Awake"), 1);
        bool afterWaking = inventories.UseItem(owner, coffee);

        asleep.Should().BeFalse();
        afterWaking.Should().BeTrue();
    }

    [Fact]
    public void ChargesAreSpentBeforeTheUnitAndAPartlyUsedUnitStandsAlone()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition bottle = InventorySystemTestsBuilder.Usable("Fact.Sips", charges: 2);
        inventories.AddItem(owner, bottle, 3);

        inventories.UseItem(owner, bottle);
        List<int> afterSip = inventories.GetStacks(owner).Select(static stack => stack.Quantity).ToList();
        int usesLeft = inventories.GetStacks(owner)[0].UsesLeft;
        inventories.UseItem(owner, bottle);

        afterSip.Should().Equal(1, 2);
        usesLeft.Should().Be(1);
        _builder.Game.Facts.GetFact(new Tag("Fact.Sips")).Should().Be(2);
        inventories.GetStacks(owner).Should().ContainSingle().Which.Quantity.Should().Be(2);
        inventories.GetStacks(owner)[0].IsPlain.Should().BeTrue();
    }

    [Fact]
    public void ItemsKeptOnUseStay()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition flute = InventorySystemTestsBuilder.Usable("Fact.Played", consumeOnUse: false);
        inventories.AddItem(owner, flute, 1);

        inventories.UseItem(owner, flute);
        inventories.UseItem(owner, flute);

        _builder.Game.Facts.GetFact(new Tag("Fact.Played")).Should().Be(2);
        inventories.GetQuantity(owner, flute).Should().Be(1);
    }

    [Fact]
    public void TransfersThePlainUnitsThatFit()
    {
        InventorySystem inventories = _builder.WithLimits(weight: true, space: false, weightLimit: 4, pocketCells: 0).Build().Inventories;
        Entity chest = _builder.Owner("\"applyCarryLimits\": false");
        Entity hero = _builder.Owner();
        ItemDefinition stone = InventorySystemTestsBuilder.Item(weight: 1);
        inventories.AddItem(chest, stone, 10);
        Guid id = inventories.GetStacks(chest)[0].Id;

        int moved = inventories.TransferStack(chest, hero, id, 10);

        moved.Should().Be(4);
        inventories.GetQuantity(hero, stone).Should().Be(4);
        inventories.GetQuantity(chest, stone).Should().Be(6);
    }

    [Fact]
    public void TransferringAsStolenMarksTheUnitsWithTheirOwner()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity shelf = _builder.Owner();
        Entity hero = _builder.Owner();
        ItemDefinition watch = InventorySystemTestsBuilder.Item();
        inventories.AddItem(shelf, watch, 3);
        Guid id = inventories.GetStacks(shelf)[0].Id;

        int moved = inventories.TransferStackAsStolen(shelf, hero, id, 2, new Tag("Faction.Merchants"));

        moved.Should().Be(2);
        ItemStack taken = inventories.GetStacks(hero).Should().ContainSingle().Subject;
        taken.Quantity.Should().Be(2);
        taken.FindData<Stolen>().Should().Be(new Stolen(new Tag("Faction.Merchants")));
        inventories.GetStacks(shelf)[0].IsPlain.Should().BeTrue();
    }

    [Fact]
    public void TransferringEverythingMovesTheStacksAndTheMoney()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity body = _builder.Owner("\"money\": 15");
        Entity hero = _builder.Owner("\"money\": 5");
        ItemDefinition coin = InventorySystemTestsBuilder.Item();
        ItemDefinition knife = InventorySystemTestsBuilder.Item();
        inventories.AddItem(body, coin, 3);
        inventories.AddItem(body, knife, 1);

        int moved = inventories.TransferAll(body, hero);

        moved.Should().Be(2);
        inventories.GetStacks(body).Should().BeEmpty();
        inventories.GetMoney(body).Should().Be(0);
        inventories.GetMoney(hero).Should().Be(20);
        inventories.GetQuantity(hero, coin).Should().Be(3);
    }

    [Fact]
    public void ReceivesAPartlyUsedUnitFirstAndAlone()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity hero = _builder.Owner();
        ItemDefinition bottle = InventorySystemTestsBuilder.Usable("Fact.Sips", charges: 4);
        ItemStack found = new(bottle, 3);
        found.SetData(new ChargesLeft(1));

        int taken = inventories.Receive(hero, found);

        taken.Should().Be(3);
        IReadOnlyList<ItemStack> stacks = inventories.GetStacks(hero);
        stacks.Select(static stack => stack.Quantity).Should().Equal(1, 2);
        stacks[0].UsesLeft.Should().Be(1);
        stacks[1].UsesLeft.Should().Be(4);
    }

    [Fact]
    public void StartsWithTheItemsAndMoneyItsSceneNames()
    {
        _builder.WithFile("Items/Bread.fvdata", """{ "type": "item", "weight": 0.5, "maxQuantity": 2 }""").Build();
        InventorySystem inventories = _builder.Inventories;

        Entity owner = _builder.Owner("""
            "items": [ { "item": "Items/Bread.fvdata", "quantity": 3 } ], "money": 12
            """);

        inventories.GetStacks(owner).Select(static stack => stack.Quantity).Should().Equal(2, 1);
        inventories.GetTotalWeight(owner).Should().Be(1.5f);
        inventories.GetMoney(owner).Should().Be(12);
    }

    [Fact]
    public void SendsTheStacksAndTotalsThatChange()
    {
        InventorySystem inventories = _builder.Build().Inventories;
        Entity owner = _builder.Owner();
        ItemDefinition item = InventorySystemTestsBuilder.Item(maxQuantity: 2);
        List<ItemChangedEvent> items = _builder.Collect<ItemChangedEvent>();
        List<StackChangedEvent> stacks = _builder.Collect<StackChangedEvent>();

        inventories.AddItem(owner, item, 3);
        inventories.RemoveItem(owner, item, 1);
        _builder.Game.Events.Dispatch();

        items.Should().Equal(new ItemChangedEvent(owner, item, 0, 3), new ItemChangedEvent(owner, item, 3, 2));
        stacks.Should().HaveCount(3);
    }
}
