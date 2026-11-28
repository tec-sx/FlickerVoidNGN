using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Core.Lists;

namespace FlickerVoid.Editor.Controls.Lists;

/// <summary>
/// A wrapping row of filter pills with counts above a list (rule R28): one chosen or none, the chosen one washed in the accent
/// with its border, an add pill at the end (dashed) that runs <see cref="AddCommand"/>, and a menu on each pill (rename, delete).
/// 20 px pills, radius 10. Its rows are <see cref="FilterPill"/>s.
/// </summary>
public class PillFilter : ListBox
{
    public const double PillHeight = 20;

    public static readonly StyledProperty<ICommand?> AddCommandProperty = AvaloniaProperty.Register<PillFilter, ICommand?>(nameof(AddCommand));

    public static readonly StyledProperty<FlyoutBase?> PillMenuProperty = AvaloniaProperty.Register<PillFilter, FlyoutBase?>(nameof(PillMenu));

    public PillFilter()
    {
        SelectionMode = SelectionMode.Single | SelectionMode.Toggle;
        AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>What the add pill runs.</summary>
    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    /// <summary>The menu a right click on a pill opens, with the pill as its data.</summary>
    public FlyoutBase? PillMenu
    {
        get => GetValue(PillMenuProperty);
        set => SetValue(PillMenuProperty, value);
    }

    /// <summary>Marks the add pill's container, so the theme draws it dashed.</summary>
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        container.Classes.Set("add", item is FilterPill { IsAdd: true });
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } container)
        {
            return;
        }

        if (container.DataContext is not FilterPill pill)
        {
            return;
        }

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (pill.IsAdd)
        {
            if (properties.IsLeftButtonPressed && AddCommand is { } add && add.CanExecute(null))
            {
                add.Execute(null);
            }

            e.Handled = true;
            return;
        }

        if (!properties.IsRightButtonPressed || PillMenu is not { } menu)
        {
            return;
        }

        if (menu is PopupFlyoutBase popup)
        {
            popup.Placement = PlacementMode.Pointer;
        }

        menu.ShowAt(container);
        e.Handled = true;
    }
}
