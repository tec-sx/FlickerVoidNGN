using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using FlickerVoid.Engine.Editor.Materials;

namespace FlickerVoid.Engine.Editor.Shell;

/// <summary>The editor's main window: a toolbar, the scene view, the inspector and a status bar; opens the material editor.</summary>
public sealed partial class MainWindow : Window
{
    private MaterialEditorWindow? _materials;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void HandleMaterialsClick(object? sender, RoutedEventArgs e)
    {
        if (_materials is not null)
        {
            _materials.Activate();
            return;
        }

        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        MaterialEditorViewModel editor = main.CreateMaterialEditor();
        _materials = new MaterialEditorWindow { DataContext = editor };
        _materials.Closed += HandleMaterialsClosed;
        _materials.Show(this);
    }

    private void HandleMaterialsClosed(object? sender, EventArgs e)
    {
        _materials = null;
    }
}
