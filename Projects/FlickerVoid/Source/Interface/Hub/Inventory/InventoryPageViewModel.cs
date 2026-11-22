using System.Collections.ObjectModel;
using System.Globalization;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Inventory.Inventories;
using FlickerVoid.Engine.Inventory.Items;
using FlickerVoid.Engine.Inventory.Outfits;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Interface.Hub.Inventory;

/// <summary>
/// The hub's inventory page: the carried weight and money, a grid of the carried stacks filled up to the inventory's cells, the
/// outfit slots, and the selected stack with Use and Wear or Take off. Clicking a slot wears the selected stack there when it fits,
/// otherwise selects what is worn there.
/// </summary>
public sealed partial class InventoryPageViewModel : HubPageViewModel
{
    private readonly InventorySystem _inventories;
    private readonly EquipmentSystem _equipment;
    private Guid _selectedStack;
    private Tag _selectedSlot;

    public InventoryPageViewModel(Game game)
        : base("Inventory")
    {
        _inventories = game.Services.GetService<InventorySystem>()!;
        _equipment = game.Services.GetService<EquipmentSystem>()!;
        game.Events.Handle((in StackChangedEvent e) => RefreshFor(e.Owner));
        game.Events.Handle((in MoneyChangedEvent e) => RefreshFor(e.Owner));
        game.Events.Handle((in CapacityChangedEvent e) => RefreshFor(e.Owner));
        game.Events.Handle((in EquipmentChangedEvent e) => RefreshFor(e.Owner));
    }

    /// <summary>Two columns of three, the way the page lays them out.</summary>
    public static IReadOnlyList<OutfitSlot> OutfitSlots { get; } =
    [
        new(new Tag("Equipment.Slot.Head"), "Head"),
        new(new Tag("Equipment.Slot.UpperBody"), "Upper Body"),
        new(new Tag("Equipment.Slot.Coat"), "Coat"),
        new(new Tag("Equipment.Slot.LowerBody"), "Lower Body"),
        new(new Tag("Equipment.Slot.Shoes"), "Shoes"),
        new(new Tag("Equipment.Slot.Storage"), "Storage"),
    ];

    public ObservableCollection<InventoryCellViewModel> Cells { get; } = [];

    public ObservableCollection<OutfitSlotViewModel> Slots { get; } = [];

    [ObservableProperty]
    public partial string Weight { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Money { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial InventoryCellViewModel? Selected { get; private set; }

    [ObservableProperty]
    public partial string SelectedName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedDescription { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanUse { get; private set; }

    [ObservableProperty]
    public partial bool CanWear { get; private set; }

    [ObservableProperty]
    public partial string WearLabel { get; private set; } = "Wear";

    protected override void Refresh()
    {
        float weight = _inventories.GetTotalWeight(Owner);
        float limit = _inventories.GetWeightLimit(Owner);
        int money = _inventories.GetMoney(Owner);
        Weight = string.Create(CultureInfo.InvariantCulture, $"{weight:0.#} / {limit:0.#} kg");
        Money = money.ToString(CultureInfo.InvariantCulture);
        RefreshCells();
        RefreshSlots();
        RefreshSelected();
    }

    [RelayCommand]
    private void Use()
    {
        _inventories.UseStack(Owner, _selectedStack);
        Refresh();
    }

    [RelayCommand]
    private void ToggleWorn()
    {
        Tag worn = FindWornSlot(_selectedStack);
        if (worn.IsValid)
        {
            _equipment.Unequip(Owner, worn);
        }
        else
        {
            _equipment.EquipStack(Owner, _selectedStack);
        }

        _selectedSlot = FindWornSlot(_selectedStack);
        Refresh();
    }

    private void SelectCell(InventoryCellViewModel cell)
    {
        _selectedStack = cell.Stack?.Id ?? Guid.Empty;
        _selectedSlot = FindWornSlot(_selectedStack);
        Refresh();
    }

    private void SelectSlot(OutfitSlotViewModel slot)
    {
        Tag tag = slot.Slot.Slot;
        bool carried = _selectedStack != Guid.Empty && !FindWornSlot(_selectedStack).IsValid;
        if (carried && _equipment.CanEquipStack(Owner, _selectedStack, tag))
        {
            _equipment.EquipStack(Owner, _selectedStack, tag);
        }
        else
        {
            _selectedStack = _equipment.GetEquippedStack(Owner, tag);
        }

        _selectedSlot = tag;
        Refresh();
    }

    private void RefreshCells()
    {
        Cells.Clear();
        IReadOnlyList<ItemStack> stacks = _inventories.GetStacks(Owner);
        foreach (ItemStack stack in stacks)
        {
            bool worn = _equipment.IsStackEquipped(Owner, stack.Id);
            Cells.Add(new InventoryCellViewModel(stack, worn, SelectCell) { IsSelected = stack.Id == _selectedStack });
        }

        int capacity = _inventories.GetCellCapacity(Owner);
        while (Cells.Count < capacity)
        {
            Cells.Add(new InventoryCellViewModel(null, false, SelectCell));
        }
    }

    private void RefreshSlots()
    {
        Slots.Clear();
        bool carried = _selectedStack != Guid.Empty && !FindWornSlot(_selectedStack).IsValid;
        foreach (OutfitSlot slot in OutfitSlots)
        {
            ItemDefinition? shown = _equipment.GetEquipped(Owner, slot.Slot);
            bool blocked = false;
            if (shown is null)
            {
                shown = _equipment.GetBlockingItem(Owner, slot.Slot);
                blocked = shown is not null;
            }

            string name = string.Empty;
            if (shown is not null)
            {
                name = DisplayNames.Of(shown);
            }

            bool canTake = carried && _equipment.CanEquipStack(Owner, _selectedStack, slot.Slot);
            Slots.Add(new OutfitSlotViewModel(slot, name, blocked, slot.Slot == _selectedSlot, canTake, SelectSlot));
        }
    }

    private void RefreshSelected()
    {
        Selected = Cells.FirstOrDefault(cell => cell.IsSelected);
        ItemStack? stack = Selected?.Stack;
        if (stack is null)
        {
            _selectedStack = Guid.Empty;
            SelectedName = string.Empty;
            SelectedDescription = string.Empty;
            CanUse = false;
            CanWear = false;
            WearLabel = "Wear";
            return;
        }

        ShowItemText(stack.Item);
        CanUse = stack.Item.IsUsable && _inventories.CanUseStack(Owner, stack.Id);
        Tag worn = FindWornSlot(stack.Id);
        if (worn.IsValid)
        {
            CanWear = _equipment.CanUnequip(Owner, worn);
            WearLabel = "Take off";
        }
        else
        {
            CanWear = _equipment.CanEquipStack(Owner, stack.Id);
            WearLabel = "Wear";
        }
    }

    private void ShowItemText(ItemDefinition item)
    {
        SelectedName = DisplayNames.Of(item);
        SelectedDescription = item.Display.LongDescription;
        if (SelectedDescription.Length == 0)
        {
            SelectedDescription = item.Display.ShortDescription;
        }
    }

    private Tag FindWornSlot(Guid stack)
    {
        if (stack == Guid.Empty)
        {
            return Tag.None;
        }

        IReadOnlyList<EquippedItem> worn = _equipment.GetAllEquipped(Owner);
        EquippedItem? found = worn.FirstOrDefault(item => item.Stack == stack);
        return found?.Slot ?? Tag.None;
    }
}
