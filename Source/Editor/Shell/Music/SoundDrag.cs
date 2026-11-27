using Avalonia.Input;
using Avalonia.Platform.Storage;

using FlickerVoid.Editor.Assets;

namespace FlickerVoid.Editor.Music;

/// <summary>Sounds dragged to the music lanes: a content path from the editor's own lists or its content drawer, or sound files from the disk.</summary>
public static class SoundDrag
{
    private static readonly DataFormat<string> Format = DataFormat.CreateInProcessFormat<string>("FlickerVoid.Sound");

    public static Task<DragDropEffects> StartAsync(PointerPressedEventArgs pressed, string path)
    {
        DataTransfer transfer = new();
        transfer.Add(DataTransferItem.Create(Format, path));
        return DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Copy);
    }

    /// <summary>The content path of the sound file being dragged, from either source; null when the drag holds none.</summary>
    public static string? GetPath(IDataTransfer transfer)
    {
        string? own = transfer.TryGetValue(Format);
        if (own is not null)
        {
            return own;
        }

        AssetEntry? asset = AssetDrag.GetEntry(transfer);
        if (asset is not null && (asset.Type == AssetTypes.Audio))
        {
            return asset.Path;
        }

        return null;
    }

    /// <summary>The paths on the disk of the sound files being dragged in from outside the editor.</summary>
    public static IReadOnlyList<string> GetFiles(IDataTransfer transfer)
    {
        List<string> paths = [];
        IEnumerable<IStorageItem>? items = transfer.TryGetFiles();
        if (items is null)
        {
            return paths;
        }

        foreach (IStorageItem item in items)
        {
            string? local = item.TryGetLocalPath();
            if (local is not null && (local.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)))
            {
                paths.Add(local);
            }
        }

        return paths;
    }
}
