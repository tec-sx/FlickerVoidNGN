using Arch.Core;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Scenes;

namespace FlickerVoid.Editor.Tests.Scenes;

/// <summary>Shows the scene outline and the Entity tab side by side in a headless window, over a scene of two trees and a lamp.</summary>
internal sealed class SceneTreeViewTestsBuilder : IDisposable
{
    private const string Street = """
        { "entities": [
          { "name": "Inn", "folder": "Street", "components": { "transform": {} } },
          { "name": "Inn Door", "parent": "Inn", "components": { "transform": {} } },
          { "name": "House", "folder": "Street", "components": { "transform": {} } },
          { "name": "House Door", "parent": "House", "components": { "transform": {} } },
          { "name": "Lamp", "components": { "transform": {} } }
        ] }
        """;

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(App)));

    private readonly EditorSceneTestsBuilder _scene = new();
    private Window? _window;

    public EditorScene Scene => _scene.Scene;

    public SceneTreeView Outline { get; private set; } = null!;

    public EntityTreeView EntityTab { get; private set; } = null!;

    public Entity Named(string name) => Scene.Find(name)!.Value;

    /// <summary>Runs <paramref name="test"/> on the headless UI thread with the street open in both trees.</summary>
    public async Task Run(Action test)
    {
        await Session.Value.Dispatch(
            () =>
            {
                Show();
                test();
                _window!.Close();
            },
            CancellationToken.None);
    }

    /// <summary>Lets the views catch up with the selection, as the next frames would.</summary>
    public static void Settle() => Dispatcher.UIThread.RunJobs();

    public static IEnumerable<string> SelectedNames(Control view)
    {
        TreeView tree = view.GetControl<TreeView>("Tree");
        return tree.SelectedItems.OfType<SceneTreeItem>().Select(item => item.Name);
    }

    public void Dispose()
    {
        _scene.Dispose();
    }

    private void Show()
    {
        _scene.WithScene("Street.fvscene", Street).Open("Street.fvscene");
        Outline = new SceneTreeView { DataContext = new SceneTreeViewModel(Scene) };
        EntityTab = new EntityTreeView { DataContext = new EntityTreeViewModel(Scene) };
        Grid trees = new() { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(EntityTab, 1);
        trees.Children.Add(Outline);
        trees.Children.Add(EntityTab);
        _window = new Window { Content = trees, Width = 600, Height = 400 };
        _window.Show();
        Settle();
    }
}
