using Arch.Core;

using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Outfits;
using FlickerVoid.Engine.Inventory.Rules;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Inventory.Tests.Outfits;

public sealed class EquipmentSystemTests : IDisposable
{
    private readonly EquipmentSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ASaveBringsBackWhatIsCarriedAndWornAndTheMoney()
    {
        _builder.Build("Coat 1, Apple 1");
        Entity hero = _builder.Hero;
        ItemStack coat = _builder.Inventories.GetStacks(hero)[0];
        ItemStack apple = _builder.Inventories.GetStacks(hero)[1];
        _builder.Inventories.SetInstanceData(hero, apple.Id, [new Stolen(new Tag("Faction.Inn"))]);
        _builder.Inventories.AddMoney(hero, 7);
        _builder.Equipment.EquipStack(hero, coat.Id);
        _builder.Saves.Save("Slot1");
        _builder.Equipment.Unequip(hero, EquipmentSystemTestsBuilder.Slot("Torso"));
        _builder.Inventories.RemoveStack(hero, apple.Id);
        _builder.Inventories.RemoveMoney(hero, 7);

        _builder.Saves.Load("Slot1");

        IReadOnlyList<ItemStack> stacks = _builder.Inventories.GetStacks(hero);
        stacks.Select(stack => stack.Id).Should().Equal(coat.Id, apple.Id);
        stacks[1].FindData<Stolen>().Should().Be(new Stolen(new Tag("Faction.Inn")));
        _builder.Inventories.GetMoney(hero).Should().Be(7);
        _builder.Equipment.GetEquippedStack(hero, EquipmentSystemTestsBuilder.Slot("Torso")).Should().Be(coat.Id);
        _builder.Game.World.Get<OwnedTags>(hero).Tags.Should().Equal(new Tag("Disguise.Police"));
    }

    [Fact]
    public void WearingAnItemGrantsItsTags()
    {
        _builder.Build("Coat 1");
        ItemDefinition coat = _builder.Item("Coat");

        bool worn = _builder.Equipment.Equip(_builder.Hero, coat);

        worn.Should().BeTrue();
        _builder.Equipment.GetEquipped(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Torso")).Should().BeSameAs(coat);
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).Tags.Should().Equal(new Tag("Disguise.Police"));
        ConditionContext context = new(_builder.Game.Facts) { Instigator = new WorldEntityView(_builder.Game.World, _builder.Hero) };
        new IsWearingCondition { GrantedTag = new Tag("Disguise") }.Check(context).Should().BeTrue();
    }

    [Fact]
    public void OnlyCarriedItemsCanBeWorn()
    {
        _builder.Build(string.Empty);

        bool worn = _builder.Equipment.Equip(_builder.Hero, _builder.Item("Coat"));

        worn.Should().BeFalse();
    }

    [Fact]
    public void WhatConflictsComesOff()
    {
        _builder.Build("Trousers 1, Dress 1");
        ItemDefinition trousers = _builder.Item("Trousers");
        ItemDefinition dress = _builder.Item("Dress");
        _builder.Equipment.Equip(_builder.Hero, trousers);
        _builder.Game.Events.Dispatch();
        List<EquipmentChangedEvent> changes = _builder.Collect<EquipmentChangedEvent>();

        _builder.Equipment.Equip(_builder.Hero, dress);
        _builder.Game.Events.Dispatch();

        changes.Should().Equal(
            new EquipmentChangedEvent(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Legs"), trousers, null),
            new EquipmentChangedEvent(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Torso"), null, dress));
        _builder.Equipment.GetBlockingItem(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Legs")).Should().BeSameAs(dress);
        _builder.Equipment.CanEquip(_builder.Hero, trousers).Should().BeTrue("putting them on again takes the dress off");
    }

    [Fact]
    public void AnItemGoesIntoTheFirstOfItsSlotsThatIsFree()
    {
        _builder.Build("Bracelet 2");
        ItemDefinition bracelet = _builder.Item("Bracelet");

        _builder.Equipment.Equip(_builder.Hero, bracelet);
        _builder.Equipment.Equip(_builder.Hero, bracelet);
        bool third = _builder.Equipment.Equip(_builder.Hero, bracelet);

        IEnumerable<EquippedItem> worn = _builder.Equipment.GetAllEquipped(_builder.Hero);
        worn.Select(static entry => entry.Slot).Should().Equal(EquipmentSystemTestsBuilder.Slot("LeftArm"), EquipmentSystemTestsBuilder.Slot("RightArm"));
        third.Should().BeFalse("both carried bracelets are worn");
    }

    [Fact]
    public void AWornStackMovesOnlyToANamedSlot()
    {
        _builder.Build("Bracelet 1");
        _builder.Equipment.Equip(_builder.Hero, _builder.Item("Bracelet"));
        Guid stack = _builder.Equipment.GetEquippedStack(_builder.Hero, EquipmentSystemTestsBuilder.Slot("LeftArm"));

        bool moved = _builder.Equipment.EquipStack(_builder.Hero, stack, EquipmentSystemTestsBuilder.Slot("RightArm"));

        moved.Should().BeTrue();
        _builder.Equipment.GetAllEquipped(_builder.Hero).Should().ContainSingle().Which.Slot.Should().Be(EquipmentSystemTestsBuilder.Slot("RightArm"));
    }

    [Fact]
    public void TakingOffKeepsTheTagsOthersGave()
    {
        _builder.Build("Coat 1");
        _builder.Game.World.Add(_builder.Hero, new OwnedTags([new Tag("Trait.Tall")]));
        _builder.Equipment.Equip(_builder.Hero, _builder.Item("Coat"));

        bool off = _builder.Equipment.Unequip(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Torso"));

        off.Should().BeTrue();
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).Tags.Should().Equal(new Tag("Trait.Tall"));
    }

    [Fact]
    public void AWornBagAddsRoomAndStaysOnWhileItIsNeeded()
    {
        _builder.WithLimits(pocketCells: 1).Build("Backpack 1");
        ItemDefinition backpack = _builder.Item("Backpack");
        _builder.Equipment.Equip(_builder.Hero, backpack);

        int apples = _builder.Inventories.AddItem(_builder.Hero, _builder.Item("Apple"), 10);

        apples.Should().Be(4, "pockets and bag hold five cells, one of them the bag's own");
        _builder.Equipment.CanUnequip(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Back")).Should().BeFalse();
        _builder.Inventories.RemoveItem(_builder.Hero, backpack).Should().BeFalse();
    }

    [Fact]
    public void AWornStackThatLeavesTheInventoryComesOff()
    {
        _builder.Build("Coat 1");
        ItemDefinition coat = _builder.Item("Coat");
        _builder.Equipment.Equip(_builder.Hero, coat);

        _builder.Inventories.RemoveItem(_builder.Hero, coat);
        _builder.Game.Events.Dispatch();

        _builder.Equipment.GetAllEquipped(_builder.Hero).Should().BeEmpty();
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).Tags.Should().BeEmpty();
    }

    [Fact]
    public void WornStacksAreRemovedLastAndNeverTransferred()
    {
        _builder.Build("Coat 3");
        Entity chest = _builder.Spawn(string.Empty);
        ItemDefinition coat = _builder.Item("Coat");
        _builder.Equipment.Equip(_builder.Hero, coat);
        Guid worn = _builder.Equipment.GetEquippedStack(_builder.Hero, EquipmentSystemTestsBuilder.Slot("Torso"));

        _builder.Inventories.RemoveItem(_builder.Hero, coat);
        int moved = _builder.Inventories.TransferAll(_builder.Hero, chest);

        moved.Should().Be(1);
        _builder.Inventories.GetStacks(_builder.Hero).Should().ContainSingle().Which.Id.Should().Be(worn);
    }

    [Fact]
    public void EquipConditionsGuardWearing()
    {
        _builder.Build("Uniform 1");
        ItemDefinition uniform = _builder.Item("Uniform");

        bool before = _builder.Equipment.Equip(_builder.Hero, uniform);
        _builder.Game.Facts.SetFact(new Tag("Fact.Enlisted"), 1);
        bool after = _builder.Equipment.Equip(_builder.Hero, uniform);

        before.Should().BeFalse();
        after.Should().BeTrue();
    }

    [Fact]
    public void StartingEquipmentIsLinkedToCarriedStacks()
    {
        _builder.Build("Coat 1", """ "equipped": [ { "slot": "Slot.Torso", "item": "Items/Coat.fvdata" } ] """);

        Guid stack = _builder.Inventories.GetStacks(_builder.Hero)[0].Id;

        _builder.Equipment.IsStackEquipped(_builder.Hero, stack).Should().BeTrue();
        _builder.Game.World.Get<OwnedTags>(_builder.Hero).Tags.Should().Equal(new Tag("Disguise.Police"));
    }

    [Fact]
    public void GiveItemCanWearWhatItGives()
    {
        _builder.Build(string.Empty);
        WorldEntityView hero = new(_builder.Game.World, _builder.Hero);
        ConditionContext context = new(_builder.Game.Facts) { Instigator = hero, Services = _builder.Game.Services };

        new GiveItemEffect { Item = "Items/Coat.fvdata", Equip = true }.Apply(context);

        _builder.Equipment.IsEquipped(_builder.Hero, _builder.Item("Coat")).Should().BeTrue();
    }
}
