using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace FlickerVoid.Samples.EditorViewport;

/// <summary>A window with a side panel of Avalonia controls and the engine's viewport beside it.</summary>
public sealed class App(bool validation) : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Window CreateWindow()
    {
        TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
        ViewportControl viewport = new(validation, status);
        StackPanel panel = new()
        {
            Width = 260,
            Margin = new Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Viewport", FontSize = 18 },
                status,
            },
        };
        DockPanel.SetDock(panel, Dock.Left);
        DockPanel layout = new() { Children = { panel, viewport } };
        return new Window
        {
            Title = "FlickerVoid editor viewport",
            Width = 1280,
            Height = 720,
            Content = layout,
        };
    }
}
