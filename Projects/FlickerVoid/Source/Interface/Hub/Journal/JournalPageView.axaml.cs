using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Interface.Hub.Journal;

public sealed partial class JournalPageView : UserControl
{
    public JournalPageView() => AvaloniaXamlLoader.Load(this);
}
