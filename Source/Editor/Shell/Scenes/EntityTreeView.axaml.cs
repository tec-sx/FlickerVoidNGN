using Arch.Core;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Scenes;

namespace FlickerVoid.Editor.Scenes;

/// <summary>
/// The Entity tab: keeps the tree's selection and the editor's in step, nests dragged entities under the entity they land on (or
/// takes them off their parents on empty space), and deletes with Delete.
/// </summary>
public sealed partial class EntityTreeView : UserControl
{
    private const double DragThreshold = 4;

    private static readonly DataFormat<SceneTreeItem[]> Format = DataFormat.CreateInProcessFormat<SceneTreeItem[]>("FlickerVoid.EntityItems");

    private readonly TreeView _tree;
    private EntityTreeViewModel? _watched;
    private bool _syncing;
    private PointerPressedEventArgs? _pressed;

    public EntityTreeView()
    {
        AvaloniaXamlLoader.Load(this);
        _tree = this.GetControl<TreeView>("Tree");
        _tree.SelectionChanged += HandleTreeSelectionChanged;
        _tree.KeyDown += HandleKeyDown;
        _tree.AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel);
        _tree.AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        _tree.AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    /// <summary>Raised to delete the selected entities, which the viewport does.</summary>
    public event Action? DeleteRequested;

    private EntityTreeViewModel? Model => DataContext as EntityTreeViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watched is not null)
        {
            _watched.PropertyChanged -= HandleTreeChanged;
        }

        _watched = Model;
        if (_watched is not null)
        {
            _watched.PropertyChanged += HandleTreeChanged;
            ShowSelection();
        }
    }

    private void HandleTreeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EntityTreeViewModel.SelectedItems) or nameof(EntityTreeViewModel.Items))
        {
            Dispatcher.UIThread.Post(ShowSelection);
        }
    }

    private void ShowSelection()
    {
        if (Model is not { } tree)
        {
            return;
        }

        // The view sets its items itself: a new list makes the tree drop its selection, which must not read as the user's choice.
        _syncing = true;
        try
        {
            if (!ReferenceEquals(_tree.ItemsSource, tree.Items))
            {
                _tree.ItemsSource = tree.Items;
            }

            _tree.SelectedItems.Clear();
            foreach (SceneTreeItem item in tree.SelectedItems)
            {
                _tree.SelectedItems.Add(item);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void HandleTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Model is not { } tree)
        {
            return;
        }

        SceneTreeItem[] chosen = [.. _tree.SelectedItems.OfType<SceneTreeItem>()];
        tree.Choose(chosen);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteRequested?.Invoke();
            e.Handled = true;
        }
    }

    private List<Entity> GetChosenEntities()
    {
        return [.. _tree.SelectedItems.OfType<SceneTreeItem>().SelectMany(item => item.GetEntities()).Distinct()];
    }

    private void ShowOptions()
    {
        if (Model is not { } tree || _tree.SelectedItems.Count == 0)
        {
            return;
        }

        List<MenuChoice> choices =
        [
            new MenuChoice("Take Off Parent", () => tree.Nest(GetChosenEntities(), null), "Nesting", "Up"),
            new MenuChoice("Delete", () => DeleteRequested?.Invoke(), "Edit", "Delete", "Delete"),
        ];
        EditorMenus.ShowOptions(_tree, choices, MenuGrouping.Separators);
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(_tree);
        if (point.Properties.IsLeftButtonPressed && (e.Source as StyledElement)?.DataContext is SceneTreeItem)
        {
            _pressed = e;
        }
    }

    private async void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null)
        {
            return;
        }

        Point start = _pressed.GetPosition(_tree);
        Point now = e.GetPosition(_tree);
        if (Math.Abs(now.X - start.X) < DragThreshold && Math.Abs(now.Y - start.Y) < DragThreshold)
        {
            return;
        }

        PointerPressedEventArgs pressed = _pressed;
        _pressed = null;
        SceneTreeItem[] dragged = [.. _tree.SelectedItems.OfType<SceneTreeItem>()];
        if (dragged.Length == 0)
        {
            return;
        }

        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(Format, dragged));
        await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Move);
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed = null;
        if (e.InitialPressMouseButton != MouseButton.Right)
        {
            return;
        }

        if ((e.Source as StyledElement)?.DataContext is SceneTreeItem item && !_tree.SelectedItems.Contains(item))
        {
            _tree.SelectedItems.Clear();
            _tree.SelectedItems.Add(item);
        }

        ShowOptions();
        e.Handled = true;
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        bool items = e.DataTransfer.TryGetValue(Format) is not null;
        e.DragEffects = items ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(Format) is not { } dragged || Model is not { } tree)
        {
            return;
        }

        Entity? parent = ((e.Source as StyledElement)?.DataContext as SceneTreeItem)?.Entity;
        IEnumerable<Entity> entities = dragged.Where(item => item.Entity is { } entity && entity != parent).Select(item => item.Entity!.Value);
        tree.Nest(entities, parent);
        e.Handled = true;
    }
}
