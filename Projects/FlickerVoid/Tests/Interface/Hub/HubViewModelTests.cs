using FlickerVoid.Interface.Hub;
using FlickerVoid.Interface.Hub.Character;
using FlickerVoid.Interface.Hub.Inventory;
using FlickerVoid.Interface.Hub.Journal;

namespace FlickerVoid.Interface.Tests.Hub;

public sealed class HubViewModelTests : IDisposable
{
    private readonly HubViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void OpensOnTheInventoryAndSwitchesPagesAround()
    {
        HubViewModel hub = _builder.Build();

        hub.Toggle();

        hub.IsOpen.Should().BeTrue();
        hub.Pages.Select(page => page.Title).Should().Equal("Inventory", "Character", "Journal");
        hub.SelectedPage.Should().BeOfType<InventoryPageViewModel>();
        hub.PreviousPageCommand.Execute(null);
        hub.SelectedPage.Should().BeOfType<JournalPageViewModel>();
        hub.NextPageCommand.Execute(null);
        hub.NextPageCommand.Execute(null);
        hub.SelectedPage.Should().BeOfType<CharacterPageViewModel>();
        hub.Pages[1].IsSelected.Should().BeTrue();
        hub.Pages[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ClosesAndReopensOnTheLastPage()
    {
        HubViewModel hub = _builder.Build();
        hub.Toggle();
        hub.NextPageCommand.Execute(null);

        hub.CloseCommand.Execute(null);
        hub.IsOpen.Should().BeFalse();
        hub.ShownPage.Should().BeNull("a closed hub holds no page view");
        hub.Toggle();
        hub.ShownPage.Should().BeSameAs(hub.Character);

        hub.SelectedPage.Should().BeOfType<CharacterPageViewModel>();
    }

    [Fact]
    public void ShowsTheCarriedStacksInCellsUpToTheCapacity()
    {
        HubViewModel hub = _builder.Build();

        hub.Toggle();

        InventoryPageViewModel inventory = hub.Inventory;
        inventory.Cells.Should().HaveCount(6);
        inventory.Cells[0].Name.Should().Be("Mug of ale");
        inventory.Cells[0].Quantity.Should().Be("2");
        inventory.Cells[1].Name.Should().Be("Flat cap");
        inventory.Cells[1].Quantity.Should().BeEmpty();
        inventory.Cells[2].IsEmpty.Should().BeTrue();
        inventory.Weight.Should().Be("1.5 / 20 kg");
        inventory.Money.Should().Be("12");
    }

    [Fact]
    public void ListsTheOutfitSlotsInPairs()
    {
        HubViewModel hub = _builder.Build();

        hub.Toggle();

        hub.Inventory.Slots.Select(slot => slot.Label).Should().Equal("Head", "Upper Body", "Coat", "Lower Body", "Shoes", "Storage");
    }

    [Fact]
    public void DescribesTheSelectedStackAndUsesIt()
    {
        HubViewModel hub = _builder.Build();
        hub.Toggle();
        InventoryPageViewModel inventory = hub.Inventory;

        inventory.Cells[0].SelectCommand.Execute(null);

        inventory.Selected.Should().BeSameAs(inventory.Cells[0]);
        inventory.Cells[0].IsSelected.Should().BeTrue();
        inventory.SelectedName.Should().Be("Mug of ale");
        inventory.SelectedDescription.Should().Be("Dark and strong.");
        inventory.CanUse.Should().BeTrue();
        inventory.CanWear.Should().BeFalse();
        inventory.UseCommand.Execute(null);
        _builder.Game.Events.Dispatch();
        inventory.Cells[0].Stack!.Quantity.Should().Be(1);
        inventory.Selected!.Name.Should().Be("Mug of ale");
    }

    [Fact]
    public void WearsTheSelectedStackInItsSlotAndTakesItOff()
    {
        HubViewModel hub = _builder.Build();
        hub.Toggle();
        InventoryPageViewModel inventory = hub.Inventory;
        inventory.Cells[1].SelectCommand.Execute(null);

        inventory.Slots[0].CanTake.Should().BeTrue();
        inventory.Slots[1].CanTake.Should().BeFalse();
        inventory.WearLabel.Should().Be("Wear");
        inventory.ToggleWornCommand.Execute(null);
        _builder.Game.Events.Dispatch();

        inventory.Slots[0].ItemName.Should().Be("Flat cap");
        inventory.Cells[1].IsWorn.Should().BeTrue();
        inventory.WearLabel.Should().Be("Take off");
        inventory.ToggleWornCommand.Execute(null);
        _builder.Game.Events.Dispatch();
        inventory.Slots[0].ItemName.Should().BeEmpty();
    }

    [Fact]
    public void WearsTheSelectedStackInTheSlotClicked()
    {
        HubViewModel hub = _builder.Build();
        hub.Toggle();
        InventoryPageViewModel inventory = hub.Inventory;
        inventory.Cells[1].SelectCommand.Execute(null);

        inventory.Slots[0].SelectCommand.Execute(null);
        _builder.Game.Events.Dispatch();

        inventory.Slots[0].ItemName.Should().Be("Flat cap");
        inventory.Slots[0].IsSelected.Should().BeTrue();
        inventory.SelectedName.Should().Be("Flat cap");
    }

    [Fact]
    public void ShowsTheProtagonistsAttributesAndModifiers()
    {
        HubViewModel hub = _builder.Build();
        hub.Toggle();

        hub.NextPageCommand.Execute(null);

        CharacterPageViewModel character = hub.Character;
        character.Attributes.Select(attribute => attribute.Name).Should().Equal("Stealth", "Health");
        character.Attributes[0].Value.Should().Be("4");
    }

    [Fact]
    public void ListsQuestsAndKnowledgeInTheJournal()
    {
        HubViewModel hub = _builder.Build();
        _builder.StartQuest("Quest.Cellar");
        _builder.Learn("Knowledge.Cellar");
        hub.Toggle();

        hub.PreviousPageCommand.Execute(null);

        JournalPageViewModel journal = hub.Journal;
        journal.ActiveQuests.Should().ContainSingle();
        journal.ActiveQuests[0].Name.Should().Be("The cellar key");
        journal.ActiveQuests[0].Objectives.Should().Equal("Find the cellar key");
        journal.Knowledge.Should().ContainSingle();
        journal.Knowledge[0].Name.Should().Be("The cellar");
        journal.Knowledge[0].Description.Should().Be("Locked since spring.");
    }
}
