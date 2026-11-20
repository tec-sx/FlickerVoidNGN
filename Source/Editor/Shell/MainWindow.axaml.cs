using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using FlickerVoid.Engine.Editor.Materials;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: a toolbar, the scene view, the asset browser, the inspector and a status bar; opens the material editor.</summary>
public sealed partial class MainWindow : Window
{
    private MaterialEditorWindow? _materials;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel main)
        {
            main.MaterialRequested += ShowMaterial;
        }
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

    private void HandleMaterialsClosed(object? sender, EventArgs e)
    {
        _materials = null;
    }
}
