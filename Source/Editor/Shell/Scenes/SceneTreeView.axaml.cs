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
/// The scene outline: keeps the tree's selection and the editor's in step, offers folder options on right-click, renames folders
/// in place (F2), deletes with Delete and moves dragged items into folders. Refreshes twice a second, so renames in the inspector appear.
/// </summary>
public sealed partial class SceneTreeView : UserControl
{
    private const double DragThreshold = 4;

    private static readonly DataFormat<SceneTreeItem[]> Format = DataFormat.CreateInProcessFormat<SceneTreeItem[]>("FlickerVoid.SceneItems");

    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly TreeView _tree;
    private SceneTreeViewModel? _watched;
    private bool _syncing;
    private PointerPressedEventArgs? _pressed;

    public SceneTreeView()
    {
        AvaloniaXamlLoader.Load(this);
        _tree = this.GetControl<TreeView>("Tree");
        _refresh.Tick += HandleRefreshTick;
        _tree.SelectionChanged += HandleTreeSelectionChanged;
        _tree.KeyDown += HandleKeyDown;
        _tree.AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Tunnel);
        _tree.AddHandler(PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel);
        _tree.AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        _tree.AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    private SceneTreeViewModel? Outline => DataContext as SceneTreeViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _refresh.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _refresh.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watched is not null)
        {
            _watched.PropertyChanged -= HandleOutlineChanged;
        }

        _watched = Outline;
        if (_watched is not null)
        {
            _watched.PropertyChanged += HandleOutlineChanged;
            ShowSelection();
        }
    }

    private void HandleRefreshTick(object? sender, EventArgs e)
    {
        Outline?.Refresh();
    }

    private void HandleOutlineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SceneTreeViewModel.SelectedItems) or nameof(SceneTreeViewModel.Items))
        {
            Dispatcher.UIThread.Post(ShowSelection);
        }
    }

    private void ShowSelection()
    {
        if (Outline is not { } outline)
        {
            return;
        }

        _syncing = true;
        try
        {
            _tree.SelectedItems.Clear();
            foreach (SceneTreeItem item in outline.SelectedItems)
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
        if (_syncing || Outline is not { } outline)
        {
            return;
        }

        SceneTreeItem[] chosen = [.. _tree.SelectedItems.OfType<SceneTreeItem>()];
        outline.Choose(chosen);
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                RenameChosenFolder();
                e.Handled = true;
                break;
            case Key.Delete:
                DeleteChosen();
                e.Handled = true;
                break;
        }
    }

    private SceneTreeItem? ChosenFolder => _tree.SelectedItems.OfType<SceneTreeItem>().LastOrDefault(item => item.IsFolder);

    private void ShowOptions()
    {
        if (Outline is not { } outline)
        {
            return;
        }

        SceneTreeItem? folder = ChosenFolder;
        SceneTreeItem? last = _tree.SelectedItems.OfType<SceneTreeItem>().LastOrDefault();
        string? parent = folder?.Path ?? (last is { IsFolder: false, Path.Length: > 0 } ? last.Path : null);
        List<MenuChoice> choices = [];
        if (EditorMenus.GetAddChoices(this) is not null)
        {
            choices.Add(EditorMenus.AddEntry(this, "Add Entity…"));
        }

        choices.Add(new MenuChoice("New Folder", () => outline.CreateFolder(parent), "Folder", "NewFolder"));
        if (folder is not null)
        {
            choices.Add(new MenuChoice("Rename Folder", RenameChosenFolder, "Folder", "Rename", "F2"));
            choices.Add(new MenuChoice("Remove Folder", () => outline.DeleteFolder(folder.Path), "Folder", "Remove"));
        }

        if (last is not null)
        {
            choices.Add(new MenuChoice("Move to Top", () => outline.MoveTo(GetChosenEntities(), null), "Folder", "Up"));
            choices.Add(new MenuChoice("Delete", DeleteChosen, "Edit", "Delete", "Delete"));
        }

        EditorMenus.ShowOptions(_tree, choices, MenuGrouping.Separators);
    }

    private IEnumerable<Entity> GetChosenEntities()
    {
        return _tree.SelectedItems.OfType<SceneTreeItem>().SelectMany(item => item.GetEntities()).Distinct().ToList();
    }

    /// <summary>Deletes the chosen entities and folders with everything in them.</summary>
    private void DeleteChosen()
    {
        if (Outline is not { } outline || DataContext is null)
        {
            return;
        }

        SceneTreeItem[] chosen = [.. _tree.SelectedItems.OfType<SceneTreeItem>()];
        foreach (SceneTreeItem folder in chosen.Where(item => item.IsFolder))
        {
            outline.DeleteFolder(folder.Path);
        }

        DeleteRequested?.Invoke();
    }

    /// <summary>Raised to delete the selected entities, which the viewport does.</summary>
    public event Action? DeleteRequested;

    private void RenameChosenFolder()
    {
        if (ChosenFolder is not { } folder || Outline is not { } outline)
        {
            return;
        }

        Control anchor = _tree.TreeContainerFromItem(folder) ?? _tree;
        TextBox box = new() { Text = folder.Name, Width = 180 };
        Flyout flyout = new() { Content = box, Placement = PlacementMode.Bottom };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                outline.RenameFolder(folder.Path, box.Text ?? "");
                flyout.Hide();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                flyout.Hide();
                e.Handled = true;
            }
        };
        flyout.Opened += (_, _) =>
        {
            box.Focus();
            box.SelectAll();
        };
        flyout.ShowAt(anchor);
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
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            if ((e.Source as StyledElement)?.DataContext is SceneTreeItem item && !_tree.SelectedItems.Contains(item))
            {
                _tree.SelectedItems.Clear();
                _tree.SelectedItems.Add(item);
            }

            ShowOptions();
            e.Handled = true;
        }
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        bool items = e.DataTransfer.TryGetValue(Format) is not null;
        e.DragEffects = items ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Moves the dragged items into the folder dropped on, the folder of the entity dropped on, or the top.</summary>
    private void HandleDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(Format) is not { } dragged || Outline is not { } outline)
        {
            return;
        }

        SceneTreeItem? target = (e.Source as StyledElement)?.DataContext as SceneTreeItem;
        string? folder = null;
        if (target is { IsFolder: true })
        {
            folder = target.Path;
        }
        else if (target is { Path.Length: > 0 })
        {
            folder = target.Path;
        }

        List<Entity> entities = [];
        foreach (SceneTreeItem item in dragged)
        {
            if (item.IsFolder && folder is not null && (folder == item.Path || folder.StartsWith(item.Path + "/", StringComparison.Ordinal)))
            {
                continue;
            }

            if (item.IsFolder)
            {
                string moved = folder is null ? item.Name : $"{folder}/{item.Name}";
                outline.RenameFolderPath(item.Path, moved);
            }
            else if (item.Entity is { } entity)
            {
                entities.Add(entity);
            }
        }

        outline.MoveTo(entities, folder);
        e.Handled = true;
    }
}
