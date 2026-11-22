using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor.Assets;

/// <summary>The icon and colour of each kind of content file, as the asset browser shows them.</summary>
public static class AssetKindLooks
{
    public static FuncValueConverter<AssetKind, Geometry> Icon { get; } = new(GetIcon);

    public static FuncValueConverter<AssetKind, IBrush> Brush { get; } = new(GetBrush);

    /// <summary>A folder's icon in the tree: the code root and its folders look like code.</summary>
    public static FuncValueConverter<AssetFolder, Geometry> FolderIcon { get; } = new(static folder => GetIcon(GetFolderKind(folder)));

    public static FuncValueConverter<AssetFolder, IBrush> FolderBrush { get; } = new(static folder => GetBrush(GetFolderKind(folder)));

    /// <summary>The icon of the file a content path names, by its extension, for fields that name files.</summary>
    public static FuncValueConverter<string?, Geometry> PathIcon { get; } = new(static path => GetIcon(AssetCatalog.GetKind(path ?? "")));

    public static FuncValueConverter<string?, IBrush> PathBrush { get; } = new(static path => GetBrush(AssetCatalog.GetKind(path ?? "")));

    private static AssetKind GetFolderKind(AssetFolder? folder)
    {
        if (folder?.Path == AssetCatalog.CodePrefix)
        {
            return AssetKind.Code;
        }

        return AssetKind.Folder;
    }

    private static Geometry GetIcon(AssetKind kind)
    {
        switch (kind)
        {
            case AssetKind.Folder:
                return MaterialIcons.Folder;
            case AssetKind.Scene:
                return MaterialIcons.Scene;
            case AssetKind.Model:
                return MaterialIcons.Model;
            case AssetKind.Material:
                return MaterialIcons.Materials;
            case AssetKind.Texture:
                return MaterialIcons.Texture;
            case AssetKind.Mood:
                return MaterialIcons.Mood;
            case AssetKind.Shader:
                return MaterialIcons.Shader;
            case AssetKind.Data:
                return MaterialIcons.Data;
            case AssetKind.Table:
                return MaterialIcons.Table;
            case AssetKind.Flow:
                return MaterialIcons.Flow;
            case AssetKind.Code:
                return MaterialIcons.Code;
            default:
                return MaterialIcons.File;
        }
    }

    private static IBrush GetBrush(AssetKind kind)
    {
        switch (kind)
        {
            case AssetKind.Folder:
                return new SolidColorBrush(Color.Parse("#DCBC98"));
            case AssetKind.Scene:
                return new SolidColorBrush(Color.Parse("#A6C33F"));
            case AssetKind.Model:
                return new SolidColorBrush(Color.Parse("#4A9BBF"));
            case AssetKind.Material:
                return new SolidColorBrush(Color.Parse("#D8955A"));
            case AssetKind.Texture:
                return new SolidColorBrush(Color.Parse("#C8553D"));
            case AssetKind.Mood:
                return new SolidColorBrush(Color.Parse("#E0AC2C"));
            case AssetKind.Shader:
                return new SolidColorBrush(Color.Parse("#B9C5CF"));
            case AssetKind.Data:
                return new SolidColorBrush(Color.Parse("#8FB8A0"));
            case AssetKind.Table:
                return new SolidColorBrush(Color.Parse("#7FA6C9"));
            case AssetKind.Flow:
                return new SolidColorBrush(Color.Parse("#B48EC9"));
            case AssetKind.Code:
                return new SolidColorBrush(Color.Parse("#6FC2B0"));
            default:
                return new SolidColorBrush(Color.Parse("#9C9A88"));
        }
    }
}
