using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Rules;

namespace FlickerVoid.Engine.Inventory.Tests.Rules;

public sealed class InventoryRulesTests : IDisposable
{
    private const string TwoCoins = """ "items": [ { "item": "Items/Coin.fvdata", "quantity": 2 } ] """;

    private readonly InventoryRulesTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HasItemReadsTheInstigatorOrTheTarget()
    {
        _builder.Build(hero: TwoCoins);
        ConditionContext context = _builder.Context();

        bool heroHasTwo = new HasItemCondition { Item = "Items/Coin.fvdata", Quantity = 2 }.Check(context);
        bool heroHasThree = new HasItemCondition { Item = "Items/Coin.fvdata", Quantity = 3 }.Check(context);
        bool merchantHasOne = new HasItemCondition { Item = "Items/Coin.fvdata", Subject = ContextEntity.Target }.Check(context);

        heroHasTwo.Should().BeTrue();
        heroHasThree.Should().BeFalse();
        merchantHasOne.Should().BeFalse();
    }

    [Fact]
    public void HasItemWithTagCountsItemsUnderTheTag()
    {
        _builder.Build(hero: TwoCoins);
        ConditionContext context = _builder.Context();

        bool two = new HasItemWithTagCondition { Tag = new Tag("Item"), Quantity = 2 }.Check(context);
        bool keys = new HasItemWithTagCondition { Tag = new Tag("Item.Key") }.Check(context);

        two.Should().BeTrue();
        keys.Should().BeFalse();
    }

    [Fact]
    public void HasMoneyNeedsTheAmount()
    {
        _builder.Build(hero: "\"money\": 5");
        ConditionContext context = _builder.Context();

        bool five = new HasMoneyCondition { Amount = 5 }.Check(context);
        bool six = new HasMoneyCondition { Amount = 6 }.Check(context);

        five.Should().BeTrue();
        six.Should().BeFalse();
    }

    [Fact]
    public void ItemsAreGivenToAndTakenFromTheSubject()
    {
        _builder.Build(hero: TwoCoins);
        ConditionContext context = _builder.Context();

        new GiveItemEffect { Item = "Items/Coin.fvdata", Quantity = 3, Subject = ContextEntity.Target }.Apply(context);
        new TakeItemEffect { Item = "Items/Coin.fvdata", Quantity = 3 }.Apply(context);
        new TakeItemEffect { Item = "Items/Coin.fvdata", Quantity = 1 }.Apply(context);

        _builder.Inventories.GetQuantity(_builder.Merchant, _builder.Coin).Should().Be(3);
        _builder.Inventories.GetQuantity(_builder.Hero, _builder.Coin).Should().Be(1, "taking three of two takes nothing");
    }

    [Fact]
    public void MoneyIsGivenAndTakenOnlyWhenThereIsEnough()
    {
        _builder.Build(hero: "\"money\": 5");
        ConditionContext context = _builder.Context();

        new TakeMoneyEffect { Amount = 6 }.Apply(context);
        new TakeMoneyEffect { Amount = 2 }.Apply(context);
        new GiveMoneyEffect { Amount = 4, Subject = ContextEntity.Target }.Apply(context);

        _builder.Inventories.GetMoney(_builder.Hero).Should().Be(3);
        _builder.Inventories.GetMoney(_builder.Merchant).Should().Be(4);
    }

    [Fact]
    public void DataFilesNameTheConditionsAndEffects()
    {
        _builder.WithFile("Items/Purse.fvdata", """
            {
              "type": "item",
              "fragments": [ {
                "$type": "usable",
                "useConditions": { "conditions": [ { "$type": "hasItemWithTag", "tag": "Item.Coin" } ] },
                "effects": { "effects": [ { "$type": "giveMoney", "amount": 3 }, { "$type": "takeItem", "item": "Items/Coin.fvdata" } ] }
              } ]
            }
            """).Build(hero: """ "items": [ { "item": "Items/Purse.fvdata" }, { "item": "Items/Coin.fvdata" } ] """);
        Guid purse = _builder.Inventories.GetStacks(_builder.Hero)[0].Id;

        bool first = _builder.Inventories.UseStack(_builder.Hero, purse);
        bool second = _builder.Inventories.CanUseStack(_builder.Hero, purse);

        first.Should().BeTrue();
        second.Should().BeFalse("the purse was used up, as was the coin it needs");
        _builder.Inventories.GetMoney(_builder.Hero).Should().Be(3);
        _builder.Inventories.GetStacks(_builder.Hero).Should().BeEmpty();
    }
}
