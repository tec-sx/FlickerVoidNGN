using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using FlickerVoid.Editor.Core.Music;

namespace FlickerVoid.Editor.Music;

/// <summary>The music editor's side column: the Layer and Arrangement tabs over the lists of sounds and arrangements.</summary>
public partial class MusicSideView : UserControl
{
    private PointerPressedEventArgs? _pressed;

    public MusicSideView()
    {
        InitializeComponent();
    }

    private MusicEditorViewModel? Editor => DataContext as MusicEditorViewModel;

    private void HandleLayerTab(object? sender, RoutedEventArgs args) => Set(editor => editor.SideTab = 0);

    private void HandleArrangementTab(object? sender, RoutedEventArgs args) => Set(editor => editor.SideTab = 1);

    private void HandleSoundsTab(object? sender, RoutedEventArgs args) => Set(editor => editor.ListTab = 0);

    private void HandleCuesTab(object? sender, RoutedEventArgs args) => Set(editor => editor.ListTab = 1);

    private void Set(Action<MusicEditorViewModel> change)
    {
        if (Editor is { } editor)
        {
            change(editor);
        }
    }

    private void HandleRemoveNotifier(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: NotifierViewModel notifier } && Editor is { } editor)
        {
            editor.RemoveNotifier(notifier);
        }
    }

    private void HandleRemoveSection(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: SectionViewModel section } && Editor is { } editor)
        {
            editor.RemoveSection(section);
        }
    }

    private void HandleRemoveContinuePoint(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: ContinuePointViewModel point } && Editor is { } editor)
        {
            editor.RemoveContinuePoint(point);
        }
    }

    private void HandlePushCue(object? sender, RoutedEventArgs args)
    {
        if (sender is Control { DataContext: SoundItem item } && Editor is { } editor)
        {
            editor.PushCueCommand.Execute(item.Path);
        }
    }

    private void HandleSoundDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (sender is ListBox { SelectedItem: SoundItem item } && Editor is { } editor)
        {
            if (editor.Selected is { } track)
            {
                editor.Assign(item.Path, track);
            }
            else
            {
                editor.DropSound(item.Path, int.MaxValue);
            }
        }
    }

    private void HandleCueDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (sender is ListBox { SelectedItem: SoundItem item } && Editor is { } editor)
        {
            editor.OpenFile(item.Path);
        }
    }

    private void HandleItemPressed(object? sender, PointerPressedEventArgs args)
    {
        _pressed = args;
    }

    private async void HandleItemMoved(object? sender, PointerEventArgs args)
    {
        if (_pressed is null || sender is not Control { DataContext: SoundItem item } control)
        {
            return;
        }

        Point start = _pressed.GetPosition(control);
        Point now = args.GetPosition(control);
        if (Math.Abs(now.X - start.X) < 5 && Math.Abs(now.Y - start.Y) < 5)
        {
            return;
        }

        PointerPressedEventArgs pressed = _pressed;
        _pressed = null;
        await SoundDrag.StartAsync(pressed, item.Path);
    }

    private async void HandleImport(object? sender, RoutedEventArgs args)
    {
        if (Editor is not { } editor || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        FilePickerOpenOptions options = new()
        {
            Title = "Import sound files",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Sound files") { Patterns = ["*.wav", "*.ogg"] }],
        };
        IReadOnlyList<IStorageFile> picked = await top.StorageProvider.OpenFilePickerAsync(options);
        List<string> paths = [];
        foreach (IStorageFile file in picked)
        {
            if (file.TryGetLocalPath() is { } local)
            {
                paths.Add(local);
            }
        }

        if (paths.Count > 0)
        {
            editor.Import(paths);
        }
    }
}
