using Avalonia.Interactivity;

namespace FlickerVoid.Editor.Inspector;

/// <summary>Asks the window to show a content file in the asset browser and open it.</summary>
public sealed class OpenAssetEventArgs(RoutedEvent routedEvent, string path) : RoutedEventArgs(routedEvent)
{
    public string Path { get; } = path;
}
