using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace FlickerVoid.Editor.Assets;

/// <summary>
/// The asset browser's folder tree and tiles. A double-click or Enter opens a file or folder, F2 renames and Delete deletes after
/// asking; a tile dragged a few pixels can be dropped on a folder (a tile or in the tree) to move it there, or a model on the viewport.
/// Files dropped from outside the editor, or picked with Import, are imported into the folder they land on or the selected one.
/// </summary>
public sealed partial class AssetBrowserView : UserControl
{
    private const double DragThreshold = 4;

    private readonly ListBox _list;
    private readonly TreeView _tree;
    private PointerPressedEventArgs? _pressed;
    private AssetEntry? _pressedAsset;

    public AssetBrowserView()
    {
        AvaloniaXamlLoader.Load(this);
        _list = this.GetControl<ListBox>("AssetList");
        _tree = this.GetControl<TreeView>("FolderTree");
        _list.AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        _list.AddHandler(PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Tunnel);
        _list.AddHandler(PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel);
        _list.AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        _list.AddHandler(DragDrop.DropEvent, HandleDrop);
        _tree.AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        _tree.AddHandler(DragDrop.DropEvent, HandleDrop);
        _tree.KeyDown += HandleTreeKeyDown;
    }

    private AssetBrowserViewModel? Browser => DataContext as AssetBrowserViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Browser is { } browser)
        {
            browser.PropertyChanged += HandleBrowserChanged;
        }
    }

    private void HandleBrowserChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssetBrowserViewModel.Prompt) && Browser?.Prompt is not null)
        {
            TextBox name = this.GetControl<TextBox>("PromptName");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => name.Focus());
        }
    }

    private void HandlePromptKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Browser?.ConfirmPromptCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                Browser?.CancelPromptCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void HandlePromptCreateClick(object? sender, RoutedEventArgs e) => Browser?.ConfirmPromptCommand.Execute(null);

    private void HandlePromptCancelClick(object? sender, RoutedEventArgs e) => Browser?.CancelPromptCommand.Execute(null);

    private void HandleDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (_list.SelectedItem is AssetEntry asset)
        {
            Browser?.OpenCommand.Execute(asset);
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                OpenSelected();
                e.Handled = true;
                break;
            case Key.F2:
                AskRename();
                e.Handled = true;
                break;
            case Key.Delete:
                AskDelete();
                e.Handled = true;
                break;
            case Key.Back:
                Browser?.UpCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void HandleTreeKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                HandleRenameFolderClick(sender, e);
                e.Handled = true;
                break;
            case Key.Delete:
                HandleDeleteFolderClick(sender, e);
                e.Handled = true;
                break;
        }
    }

    private async void HandleImportClick(object? sender, RoutedEventArgs e)
    {
        if (Browser is not { } browser || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        FilePickerOpenOptions options = new() { Title = "Import", AllowMultiple = true };
        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(options);
        List<string> paths = GetLocalPaths(files);
        if (paths.Count > 0)
        {
            browser.Import(paths);
        }
    }

    private void HandleRenameClick(object? sender, RoutedEventArgs e) => AskRename();

    private void HandleDeleteClick(object? sender, RoutedEventArgs e) => AskDelete();

    private void HandleRenameFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Browser is not { SelectedFolder: { } folder } browser)
        {
            return;
        }

        browser.SelectedAsset = null;
        Control anchor = FindTreeItem(folder);
        ShowRename(anchor, folder.Name);
    }

    private void HandleDeleteFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Browser is not { SelectedFolder: { } folder } browser)
        {
            return;
        }

        browser.SelectedAsset = null;
        Control anchor = FindTreeItem(folder);
        ShowDelete(anchor, folder.Name);
    }

    private void AskRename()
    {
        if (Browser?.SelectedAsset is not { } asset)
        {
            return;
        }

        Control anchor = FindTile(asset);
        ShowRename(anchor, asset.Name);
    }

    private void AskDelete()
    {
        if (Browser?.SelectedAsset is not { } asset)
        {
            return;
        }

        Control anchor = FindTile(asset);
        ShowDelete(anchor, asset.Name);
    }

    private void ShowRename(Control anchor, string name)
    {
        TextBox box = new() { Text = name, Width = 180 };
        Flyout flyout = new() { Content = box, Placement = PlacementMode.Bottom };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Browser?.RenameCommand.Execute(box.Text);
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

    private void ShowDelete(Control anchor, string name)
    {
        Button delete = new() { Content = "Delete", Classes = { "delete" } };
        TextBlock question = new() { Text = $"Delete {name}, with its source file?" };
        StackPanel panel = new() { Spacing = 4, Children = { question, delete } };
        Flyout flyout = new() { Content = panel, Placement = PlacementMode.Bottom };
        delete.Click += (_, _) =>
        {
            Browser?.DeleteCommand.Execute(null);
            flyout.Hide();
        };
        flyout.ShowAt(anchor);
    }

    private Control FindTile(AssetEntry asset)
    {
        Control? tile = _list.ContainerFromItem(asset);
        return tile ?? _list;
    }

    private Control FindTreeItem(AssetFolder folder)
    {
        Control? item = _tree.TreeContainerFromItem(folder);
        return item ?? _tree;
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _list.Focus();
        PointerPoint point = e.GetCurrentPoint(this);
        bool left = point.Properties.IsLeftButtonPressed;
        StyledElement? source = e.Source as StyledElement;
        if (left && source?.DataContext is AssetEntry asset)
        {
            _pressed = e;
            _pressedAsset = asset;
        }
    }

    private async void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed is null || _pressedAsset is null)
        {
            return;
        }

        Point start = _pressed.GetPosition(this);
        Point now = e.GetPosition(this);
        Point moved = now - start;
        if (Math.Abs(moved.X) < DragThreshold && Math.Abs(moved.Y) < DragThreshold)
        {
            return;
        }

        PointerPressedEventArgs pressed = _pressed;
        AssetEntry asset = _pressedAsset;
        _pressed = null;
        _pressedAsset = null;
        await AssetDrag.StartAsync(pressed, asset);
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed = null;
        _pressedAsset = null;
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        AssetEntry? dragged = AssetDrag.GetEntry(e.DataTransfer);
        string? folder = GetDropFolder(e);
        bool moves = dragged is not null && folder is not null && folder != dragged.Path;
        e.DragEffects = moves ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        AssetEntry? dragged = AssetDrag.GetEntry(e.DataTransfer);
        string? folder = GetDropFolder(e);
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files && Browser is { } importer)
        {
            List<string> paths = GetLocalPaths(files);
            string into = folder ?? importer.SelectedFolder?.Path ?? "";
            importer.Import(paths, into);
            e.Handled = true;
            return;
        }

        if (dragged is null || folder is null || Browser is not { } browser)
        {
            return;
        }

        browser.MoveTo(dragged, folder);
        e.Handled = true;
    }

    /// <summary>The local paths of the files among the given storage items, leaving folders out.</summary>
    private static List<string> GetLocalPaths(IEnumerable<IStorageItem> items)
    {
        List<string> paths = [];
        foreach (IStorageItem item in items)
        {
            if (item is IStorageFile && item.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>The folder under the pointer: a folder tile or a folder in the tree.</summary>
    private static string? GetDropFolder(DragEventArgs e)
    {
        StyledElement? target = e.Source as StyledElement;
        switch (target?.DataContext)
        {
            case AssetEntry tile when tile.Type == AssetTypes.Folder:
                return tile.Path;
            case AssetFolder folder:
                return folder.Path;
            default:
                return null;
        }
    }
}
