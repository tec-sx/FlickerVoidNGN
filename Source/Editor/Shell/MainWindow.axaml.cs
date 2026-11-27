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
using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Editor.Music;
using FlickerVoid.Editor.Core.Settings;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Documents;
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
    private bool _closingDecided;

    /// <summary>The project the editor reopens on once this window has closed, chosen with Open Project.</summary>
    private string? _reopenProject;

    /// <summary>Ctrl+K opens the command palette in the middle of the window.</summary>
    private void HandlePaletteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
        {
            ShowPalette();
            e.Handled = true;
        }
    }

    /// <summary>The top bar's search box opens the command palette, as Ctrl+K does.</summary>
    private void HandleCommandClick(object? sender, RoutedEventArgs e) => ShowPalette();

    /// <summary>Opens the command palette under the top bar: the view model's choices and the tabs only the window opens.</summary>
    private void ShowPalette()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        List<MenuChoice> choices = [.. viewModel.PaletteChoices];
        int first = choices.FindIndex(choice => choice.Group == "View");
        choices.Insert(first, new MenuChoice("Project settings", () => HandleProjectSettingsClick(null, new RoutedEventArgs()), "Commands", "Settings"));
        choices.Insert(first, new MenuChoice("Materials", () => HandleMaterialsClick(null, new RoutedEventArgs()), "Commands", "Materials"));
        SearchMenu.ShowPalette(this, choices);
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
            main.MusicRequested += ShowMusic;
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
        if (DataContext is MainWindowViewModel main)
        {
            EditorDocument[] open = [.. main.Documents.Documents];
            foreach (EditorDocument document in open)
            {
                main.Documents.Close(document);
            }
        }

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

    /// <summary>Shows the project settings in their document tab, opening it once.</summary>
    private void HandleProjectSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        EditorDocument? open = main.Documents.Find(ProjectSettingsKey);
        if (open is null)
        {
            ProjectSettingsViewModel settings = new(main.Viewport.Scene);
            ProjectSettingsView view = new() { DataContext = settings };
            open = new EditorDocument(ProjectSettingsKey, "Project settings", view, settings.Dispose, icon: "Settings");
        }

        main.Documents.Show(open);
    }

    /// <summary>The project settings' tab.</summary>
    private const string ProjectSettingsKey = "Project settings";

    /// <summary>The material editor's tab: one editor lists every material.</summary>
    private const string MaterialsKey = "Materials";

    private void HandleMaterialsClick(object? sender, RoutedEventArgs e)
    {
        ShowMaterial(null);
    }

    /// <summary>Shows the material editor in its document tab, opening it once, and selects the material when one is given.</summary>
    private void ShowMaterial(string? path)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        EditorDocument? open = main.Documents.Find(MaterialsKey);
        if (open is null)
        {
            MaterialEditorViewModel created = main.CreateMaterialEditor();
            MaterialEditorView view = new() { DataContext = created };
            open = new EditorDocument(MaterialsKey, "Materials", view, created.Dispose, created.SaveCommand, "Materials");
        }

        main.Documents.Show(open);
        if (path is not null && open.Content is MaterialEditorView { DataContext: MaterialEditorViewModel editor })
        {
            editor.SelectedMaterial = path;
        }
    }

    /// <summary>Shows the graph in its file's document tab, opening one unless the file has one already.</summary>
    private void ShowFlow(string path, FlowGraphViewModel graph)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        if (main.Documents.Find(path) is { Content: FlowEditorView open } document)
        {
            open.DataContext = graph;
            if (document.Side is Control shownSide)
            {
                shownSide.DataContext = graph;
            }

            main.Documents.Show(document);
            return;
        }

        FlowEditorView view = new() { DataContext = graph, History = main.GetFlowHistory(path) };
        FlowSideView side = new() { DataContext = graph };
        EditorDocument created = new(path, graph.Title, view, () => main.CloseFlow(path), icon: "Flow") { Side = side };
        main.Documents.Show(created);
    }

    /// <summary>Shows the music cue in its file's document tab, opening one unless the file has one already.</summary>
    private void ShowMusic(string path, MusicEditorViewModel editor)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        if (main.Documents.Find(path) is { Content: MusicEditorView open } document)
        {
            open.DataContext = editor;
            main.Documents.Show(document);
            return;
        }

        MusicEditorView view = new() { DataContext = editor, History = main.GetMusicHistory(path) };
        EditorDocument created = new(path, editor.Title, view, () => main.CloseMusic(path), icon: "Music");
        main.Documents.Show(created);
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

    /// <summary>Shows the texture's settings in its file's document tab.</summary>
    private void ShowTexture(TextureEditorViewModel editor)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        if (main.Documents.Find(editor.Path) is { } open)
        {
            editor.Dispose();
            main.Documents.Show(open);
            return;
        }

        TextureEditorView view = new() { DataContext = editor };
        UndoShortcuts.Attach(view, () => editor.History);
        EditorDocument created = new(editor.Path, editor.Heading, view, editor.Dispose, editor.SaveCommand, "Texture");
        main.Documents.Show(created);
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
}
