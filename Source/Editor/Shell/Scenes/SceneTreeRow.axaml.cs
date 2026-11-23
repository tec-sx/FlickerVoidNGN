using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Editor.Scenes;

/// <summary>A row of the scene outline and the Entity tab: an item's icon and name, with its reveal and hide buttons.</summary>
public sealed partial class SceneTreeRow : UserControl
{
    public SceneTreeRow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
