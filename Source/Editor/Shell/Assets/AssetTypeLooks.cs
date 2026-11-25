using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor.Assets;

/// <summary>The icon and colour of each type of content file, as the asset browser shows them.</summary>
public static class AssetTypeLooks
{
    public static FuncValueConverter<AssetType, Geometry> Icon { get; } = new(GetIcon);

    public static FuncValueConverter<AssetType, IBrush> Brush { get; } = new(GetBrush);

    /// <summary>A folder's icon in the tree: the code root and its folders look like code.</summary>
    public static FuncValueConverter<AssetFolder, Geometry> FolderIcon { get; } = new(static folder => GetIcon(GetFolderType(folder)));

    public static FuncValueConverter<AssetFolder, IBrush> FolderBrush { get; } = new(static folder => GetBrush(GetFolderType(folder)));

    /// <summary>The icon of the file a content path names, by its extension, for fields that name files.</summary>
    public static FuncValueConverter<string?, Geometry> PathIcon { get; } = new(static path => GetIcon(AssetTypes.Find(path ?? "")));

    public static FuncValueConverter<string?, IBrush> PathBrush { get; } = new(static path => GetBrush(AssetTypes.Find(path ?? "")));

    private static AssetType GetFolderType(AssetFolder? folder)
    {
        if (folder?.Path == AssetCatalog.CodePrefix)
        {
            return AssetTypes.Code;
        }

        return AssetTypes.Folder;
    }

    private static Geometry GetIcon(AssetType? type)
    {
        Geometry? icon = MaterialIcons.Find(type?.Icon);
        return icon ?? MaterialIcons.File;
    }

    private static IBrush GetBrush(AssetType? type)
    {
        AssetType shown = type ?? AssetTypes.Other;
        Color color = Color.Parse(shown.Color);
        return new SolidColorBrush(color);
    }
}
