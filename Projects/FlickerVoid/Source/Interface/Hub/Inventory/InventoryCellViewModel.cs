using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Engine.Inventory.Inventories;

namespace FlickerVoid.Interface.Hub.Inventory;

/// <summary>A cell of the inventory grid: a carried stack, or an empty cell when <see cref="Stack"/> is null.</summary>
public sealed partial class InventoryCellViewModel(ItemStack? stack, bool isWorn, Action<InventoryCellViewModel> select) : ObservableObject
{
    public ItemStack? Stack { get; } = stack;

    public bool IsEmpty => Stack is null;

    public bool IsWorn { get; } = isWorn;

    public string Name { get; } = stack is null ? string.Empty : DisplayNames.Of(stack.Item);

    /// <summary>The units in the stack when more than one.</summary>
    public string Quantity { get; } = stack is { Quantity: > 1 } ? stack.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => select(this);
}
