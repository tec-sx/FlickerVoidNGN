using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Core.Flows;
using FlickerVoid.Editor.Flows;
using FlickerVoid.Editor.Inspector;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Editor.Scenes;

namespace FlickerVoid.Editor;

/// <summary>The editor's main window: a toolbar, the scene view, the asset browser, the inspector and a status bar; opens the material editor.</summary>
public sealed partial class MainWindow : Window
{
    private MaterialEditorWindow? _materials;
    private bool _closingDecided;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        this.GetControl<SceneTreeView>("SceneTree").DeleteRequested += HandleOutlineDelete;
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
            AddHandler(AssetPickerView.OpenRequestedEvent, HandleOpenAsset);
            main.AskUnsaved = AskUnsaved;
        }
    }

    private Task<UnsavedAssetsChoice?> AskUnsaved(IReadOnlyList<string> paths)
    {
        UnsavedAssetsDialog dialog = new(paths);
        return dialog.ShowDialog<UnsavedAssetsChoice?>(this);
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

    private void ShowFlow(FlowGraphViewModel graph)
    {
        FlowEditorWindow window = new() { DataContext = graph };
        window.AddHandler(AssetPickerView.OpenRequestedEvent, HandleOpenAsset);
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
