using Avalonia.Input;

namespace FlickerVoid.Editor.Assets;

/// <summary>Content files dragged out of the asset browser, within the editor: the browser starts the drag and the viewport takes models.</summary>
public static class AssetDrag
{
    private static readonly DataFormat<AssetEntry> Format = DataFormat.CreateInProcessFormat<AssetEntry>("FlickerVoid.Asset");

    public static Task<DragDropEffects> StartAsync(PointerPressedEventArgs pressed, AssetEntry asset)
    {
        DataTransfer transfer = new();
        DataTransferItem item = DataTransferItem.Create(Format, asset);
        transfer.Add(item);
        return DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Copy);
    }

    /// <summary>The content path of the model being dragged, or null when the drag holds no model.</summary>
    public static string? GetModel(IDataTransfer transfer)
    {
        AssetEntry? asset = transfer.TryGetValue(Format);
        if (asset is not { Kind: AssetKind.Model })
        {
            return null;
        }

        return asset.Path;
    }
}
