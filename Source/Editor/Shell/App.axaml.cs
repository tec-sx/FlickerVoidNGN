using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Editor.Viewports;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>Creates the edited scene and its view models, and shows them in the main window.</summary>
public sealed partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private EditorScene? _scene;

    /// <summary>What to open, from the command line.</summary>
    public EditorOptions Options { get; init; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
            _scene = new EditorScene(_loggerFactory, Options.Content);
            if (Options.Scene is not null)
            {
                _scene.Open(Options.Scene);
            }

            ViewportViewModel viewport = new(_scene, Options.Validation);
            MainWindowViewModel main = new(viewport);
            desktop.MainWindow = new MainWindow { DataContext = main };
            desktop.Exit += HandleExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void HandleExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _scene?.Dispose();
        _loggerFactory?.Dispose();
    }
}
