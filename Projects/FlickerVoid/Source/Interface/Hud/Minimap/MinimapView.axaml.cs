using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Interface.Hud.Minimap;

public sealed partial class MinimapView : UserControl
{
    public MinimapView() => AvaloniaXamlLoader.Load(this);
}
