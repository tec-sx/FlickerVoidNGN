using Avalonia;
using Avalonia.Markup.Xaml;

namespace FlickerVoid.Interface;

/// <summary>The interface's Avalonia application: Fluent with the game's look over it. Start it with <c>UiPlatform.Start&lt;InterfaceApplication&gt;()</c>.</summary>
public sealed class InterfaceApplication : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
