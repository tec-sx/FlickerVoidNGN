using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Projects;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Editor.Inspector;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Editor.Viewports;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Editor;

/// <summary>Shows the project picker, or, given a project or content folder, the edited scene and its view models in the main window.</summary>
public sealed partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private EditorScene? _scene;
    private DispatcherTimer? _poll;

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
            desktop.Exit += HandleExit;
            if (Options.Content is null)
            {
                desktop.MainWindow = CreatePicker(desktop);
            }
            else
            {
                desktop.MainWindow = CreateEditor(Options.Content, Options.Scene, Options.Game);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The project picker, which replaces itself with the editor once a project is built.</summary>
    private Window CreatePicker(IClassicDesktopStyleApplicationLifetime desktop)
    {
        string folder = ProjectPickerViewModel.GetDefaultProjectsFolder();
        ProjectPickerViewModel picker = new(folder, new DotnetProjectBuilder(), new TemplateProjectCreator());
        ProjectPickerWindow window = new() { DataContext = picker };
        picker.Opened += project =>
        {
            Window editor = CreateEditor(project.ContentFolder, project.StartupScene, project.Program);
            desktop.MainWindow = editor;
            editor.Show();
            window.Close();
        };
        return window;
    }

    private Window CreateEditor(string content, string? scene, string? game)
    {
        UnsavedContent unsaved = new(new ContentManager(content));
        IReadOnlyList<IGameModule> modules = [];
        string? codeProblem = null;
        if (game is not null)
        {
            try
            {
                modules = GameCode.Load(game);
            }
            catch (Exception exception) when (exception is IOException or BadImageFormatException or TypeLoadException or MissingMethodException)
            {
                codeProblem = $"Can't load the game's code from {game}: {exception.Message}";
            }
        }

        _scene = new EditorScene(_loggerFactory!, unsaved, modules);
        ViewportViewModel viewport = new(_scene, Options.Validation);
        if (codeProblem is not null)
        {
            viewport.Status = codeProblem;
        }

        if (scene is not null)
        {
            try
            {
                _scene.Open(scene);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                viewport.Status = $"Can't open {scene}: {exception.Message}";
            }
        }

        ContentFileFinder files = new(_scene.Content);
        InspectorViewModel inspector = new(_scene, files.Find);
        ScenePlayer? player = null;
        if (game is not null)
        {
            player = new ScenePlayer(_scene, game, new ProcessGameLauncher());
        }

        MainWindowViewModel main = new(viewport, inspector, unsaved, player);
        _poll = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, HandlePoll);
        _poll.Start();
        return new MainWindow { DataContext = main };
    }

    private void HandlePoll(object? sender, EventArgs e)
    {
        _scene?.Poll();
    }

    private void HandleExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _poll?.Stop();
        _scene?.Dispose();
        _loggerFactory?.Dispose();
    }
}
