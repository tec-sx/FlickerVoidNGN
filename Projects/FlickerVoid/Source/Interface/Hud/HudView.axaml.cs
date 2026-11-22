using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Interface.Hud;

public sealed partial class HudView : UserControl
{
    public HudView() => AvaloniaXamlLoader.Load(this);
}
