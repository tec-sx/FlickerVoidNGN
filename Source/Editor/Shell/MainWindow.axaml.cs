using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Assets.Textures;
using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Core.Settings;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Environments;
using FlickerVoid.Editor.Flows;
using FlickerVoid.Editor.Inspector;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Editor.Settings;
using FlickerVoid.Editor.Textures;

namespace FlickerVoid.Editor;

/// <summary>The editor's main window: a toolbar, the scene view, the asset browser, the inspector and a status bar; opens the material editor.</summary>
public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, FlowEditorWindow> _flows = new(StringComparer.OrdinalIgnoreCase);
    private MaterialEditorWindow? _materials;
    private ProjectSettingsWindow? _projectSettings;
    private bool _closingDecided;

    /// <summary>The project the editor reopens on once this window has closed, chosen with Open Project.</summary>
    private string? _reopenProject;

    /// <summary>Ctrl+K opens the command palette in the middle of the window.</summary>
    private void HandlePaletteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control && DataContext is MainWindowViewModel viewModel)
        {
            IReadOnlyList<MenuChoice> choices = viewModel.PaletteChoices;
            SearchMenu.Show(this, choices, MenuGrouping.Separators, atPointer: false);
            e.Handled = true;
        }
    }

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        this.GetControl<SceneTreeView>("SceneTree").DeleteRequested += HandleOutlineDelete;
        this.GetControl<EntityTreeView>("EntityTree").DeleteRequested += HandleOutlineDelete;
        UndoShortcuts.Attach(this, FindHistory);
        AddHandler(KeyDownEvent, HandlePaletteKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, (_, _) => TrackSidePanelFocus(), RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The history of what the keyboard is in: the View or Data tab's file, else the viewport's.</summary>
    private UndoHistory? FindHistory()
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return null;
        }

        TrackSidePanelFocus();
        return main.GetHistory();
    }

    /// <summary>Tells the view model whether the keyboard is in the context panel or the outliner, so the top bar's undo and redo follow it.</summary>
    private void TrackSidePanelFocus()
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        TabControl side = this.GetControl<TabControl>("SidePanel");
        TabControl outliner = this.GetControl<TabControl>("Outliner");
        Visual? focused = FocusManager?.GetFocusedElement() as Visual;
        main.SidePanelFocused = focused is not null && side.IsVisualAncestorOf(focused);
        main.OutlinerFocused = focused is not null && outliner.IsVisualAncestorOf(focused);
    }

    private void HandleOutlineDelete()
    {
        if (DataContext is MainWindowViewModel main)
        {
            main.Viewport.DeleteSelected();
        }
    }

    /// <summary>Asks whether to save or discard the unsaved scene and assets before closing, and keeps the window open on cancel.</summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closingDecided || DataContext is not MainWindowViewModel main || !main.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        UnsavedAssetsChoice? choice = await AskUnsaved(main.UnsavedPaths);
        switch (choice)
        {
            case UnsavedAssetsChoice.Save:
                main.SaveAll();
                break;
            case UnsavedAssetsChoice.Discard:
                main.DiscardAssets();
                break;
            default:
                _reopenProject = null;
                return;
        }

        _closingDecided = true;
        Close();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel main)
        {
            main.MaterialRequested += ShowMaterial;
            main.FlowRequested += ShowFlow;
            main.EnvironmentRequested += ShowEnvironment;
            EnvironmentEditorView dayDock = this.GetControl<EnvironmentEditorView>("DayDock");
            dayDock.CloseRequested += main.CloseDayDock;
            main.TextureRequested += ShowTexture;
            AddHandler(AssetPickerView.OpenRequestedEvent, HandleOpenAsset);
            main.AskUnsaved = AskUnsaved;
        }
    }

    /// <summary>Starts the editor again on the project chosen with Open Project, as Unreal does, since a run loads one game's code.</summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_reopenProject is null)
        {
            return;
        }

        string editor = Environment.ProcessPath!;
        if (Path.GetFileNameWithoutExtension(editor) == "dotnet")
        {
            editor = typeof(MainWindow).Assembly.Location;
        }

        ProcessGameLauncher launcher = new();
        launcher.Launch(editor, [_reopenProject]);
    }

    /// <summary>Shows the project picker; the project it opens replaces this one after the unsaved prompt.</summary>
    private async void HandleOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        string folder = ProjectPickerViewModel.GetDefaultProjectsFolder();
        string recentFile = RecentProjects.GetDefaultFile();
        RecentProjects recent = new(recentFile);
        ProjectPickerViewModel picker = new(folder, recent, new DotnetProjectBuilder(), new TemplateProjectCreator());
        ProjectPickerWindow window = new() { DataContext = picker };
        string? chosen = null;
        picker.Opened += project =>
        {
            chosen = project.File;
            window.Close();
        };
        await window.ShowDialog(this);
        if (chosen is null)
        {
            return;
        }

        _reopenProject = chosen;
        Close();
    }

    private Task<UnsavedAssetsChoice?> AskUnsaved(IReadOnlyList<string> paths)
    {
        UnsavedAssetsDialog dialog = new(paths);
        return dialog.ShowDialog<UnsavedAssetsChoice?>(this);
    }

    /// <summary>Shows the project settings window, opening it once.</summary>
    private void HandleProjectSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_projectSettings is not null)
        {
            _projectSettings.Activate();
            return;
        }

        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        ProjectSettingsViewModel settings = new(main.Viewport.Scene);
        _projectSettings = new ProjectSettingsWindow { DataContext = settings };
        _projectSettings.Closed += (_, _) =>
        {
            settings.Dispose();
            _projectSettings = null;
        };
        _projectSettings.Show(this);
    }

    private void HandleMaterialsClick(object? sender, RoutedEventArgs e)
    {
        ShowMaterial(null);
    }

    /// <summary>Shows the material editor, opening it once, and selects the material when one is given.</summary>
    private void ShowMaterial(string? path)
    {
        if (_materials is null)
        {
            if (DataContext is not MainWindowViewModel main)
            {
                return;
            }

            MaterialEditorViewModel created = main.CreateMaterialEditor();
            _materials = new MaterialEditorWindow { DataContext = created };
            _materials.Closed += HandleMaterialsClosed;
            _materials.Show(this);
        }
        else
        {
            _materials.Activate();
        }

        if (path is not null && _materials.DataContext is MaterialEditorViewModel editor)
        {
            editor.SelectedMaterial = path;
        }
    }

    /// <summary>Shows the graph in its file's editor window, opening one unless the file has one already.</summary>
    private void ShowFlow(string path, FlowGraphViewModel graph)
    {
        if (_flows.TryGetValue(path, out FlowEditorWindow? open))
        {
            open.DataContext = graph;
            open.Activate();
            return;
        }

        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        FlowEditorWindow window = new() { DataContext = graph, History = main.GetFlowHistory(path) };
        window.AddHandler(AssetPickerView.OpenRequestedEvent, HandleOpenAsset);
        window.Closed += (_, _) =>
        {
            _flows.Remove(path);
            main.CloseFlow(path);
        };
        _flows.Add(path, window);
        window.Show(this);
    }

    /// <summary>Shows the environment in the Day dock under the viewport, undone through its file's history.</summary>
    private void ShowEnvironment(string path, EnvironmentEditorViewModel editor)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        EnvironmentEditorView dock = this.GetControl<EnvironmentEditorView>("DayDock");
        dock.History = main.GetEnvironmentHistory(path);
    }

    private void ShowTexture(TextureEditorViewModel editor)
    {
        TextureEditorWindow window = new() { DataContext = editor };
        window.AddHandler(AssetPickerView.OpenRequestedEvent, HandleOpenAsset);
        UndoShortcuts.Attach(window, () => editor.History);
        window.Closed += (_, _) => editor.Dispose();
        window.Show(this);
    }

    /// <summary>An asset field's open button, here or in a window this one opened: shows the file in the asset browser and opens it.</summary>
    private void HandleOpenAsset(object? sender, OpenAssetEventArgs e)
    {
        if (DataContext is MainWindowViewModel main)
        {
            main.OpenContentFile(e.Path);
        }

        e.Handled = true;
    }

    private void HandleMaterialsClosed(object? sender, EventArgs e)
    {
        _materials = null;
    }
}
