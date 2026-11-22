using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Interface.Hub;

public sealed partial class HubView : UserControl
{
    public HubView() => AvaloniaXamlLoader.Load(this);
}
