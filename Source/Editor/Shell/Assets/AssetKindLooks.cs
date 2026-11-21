using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor.Assets;

/// <summary>The icon and colour of each kind of content file, as the asset browser shows them.</summary>
public static class AssetKindLooks
{
    public static FuncValueConverter<AssetKind, Geometry> Icon { get; } = new(GetIcon);

    public static FuncValueConverter<AssetKind, IBrush> Brush { get; } = new(GetBrush);

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
            default:
                return new SolidColorBrush(Color.Parse("#9C9A88"));
        }
    }
}
