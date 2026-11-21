using System.Text;

using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests;

/// <summary>
/// Builds the main window's view model over a temporary content folder holding Room.fvscene and Hall.fvscene, each with a Lamp,
/// Paper.fvmat, Night.fvmood, whose exposure is 0.5, Lantern.fvdata, a lantern of 6 hours, Cellar.fvflow, a flow with a start node, and Lit.fvscene, which names Night.fvmood as its mood; the user's answer to the unsaved files prompt is <see cref="Answer"/>.
/// </summary>
internal sealed class MainWindowViewModelTestsBuilder : IDisposable
{
    public const string Room = "Room.fvscene";
    public const string Hall = "Hall.fvscene";
    public const string Paper = "Paper.fvmat";
    public const string Night = "Night.fvmood";
    public const string Lit = "Lit.fvscene";
    public const string Lantern = "Lantern.fvdata";
    public const string Cellar = "Cellar.fvflow";

    private const string LampScene = """{ "entities": [ { "name": "Lamp", "components": { "transform": {} } } ] }""";

    private readonly string _contentFolder;
    private readonly UnsavedContent _content;
    private readonly EditorScene _scene;

    public MainWindowViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        WriteFile(Room, LampScene);
        WriteFile(Cellar, """{ "nodes": [ { "$type": "start", "id": "start" } ] }""");
        WriteFile(Hall, LampScene);
        WriteFile(Paper, "{}");
        WriteFile(Night, """{ "exposure": 0.5 }""");
        WriteFile(Lit, """{ "entities": [ { "name": "Lighting", "components": { "transform": {}, "sceneMood": { "mood": "Night.fvmood" } } } ] }""");
        WriteFile(Lantern, """{ "type": "lantern", "hours": 6 }""");
        _content = new UnsavedContent(new ContentManager(_contentFolder));
        _scene = new EditorScene(NullLoggerFactory.Instance, _content);
        _scene.Game.Data.AddResolver(Data.LanternJsonContext.Default);
        _scene.Game.Data.Register<Data.Lantern>("lantern");
        _scene.Open(Room);
        ViewportViewModel viewport = new(_scene, validation: false);
        ContentFileFinder files = new(_content);
        InspectorViewModel inspector = new(_scene, files.Find);
        ViewModel = new MainWindowViewModel(viewport, inspector, _content);
        ViewModel.AskUnsaved = Ask;
    }

    public MainWindowViewModel ViewModel { get; }

    public EditorScene Scene => _scene;

    /// <summary>What the prompt answers; null cancels.</summary>
    public UnsavedAssetsChoice? Answer { get; set; } = UnsavedAssetsChoice.Save;

    /// <summary>The files each prompt listed, one list per prompt.</summary>
    public List<IReadOnlyList<string>> Prompts { get; } = [];

    /// <summary>Moves the open scene's lamp, leaving the scene unsaved.</summary>
    public void EditScene()
    {
        Entity lamp = _scene.Entities[0];
        Engine.Runtime.Components.Transform moved = new() { Position = new System.Numerics.Vector3(4, 0, 0) };
        _scene.SetComponent(lamp, moved);
    }

    /// <summary>Gives the open scene's lamp a scene mood naming the mood file, as the inspector does.</summary>
    public void NameSceneMood(string mood)
    {
        Entity lamp = _scene.Entities[0];
        SceneComponent component = _scene.Game.Spawner.Components.Single(candidate => candidate.Name == "sceneMood");
        _scene.SetComponent(lamp, component, new Engine.Rendering.Lighting.SceneMood { Mood = mood });
    }

    public void EditMaterial()
    {
        byte[] contents = Encoding.UTF8.GetBytes("""{ "edited": true }""");
        _content.Edit(Paper, contents);
    }

    public void OpenInBrowser(string path, AssetKind kind = AssetKind.Scene)
    {
        AssetEntry entry = new(path, Path.GetFileNameWithoutExtension(path), kind);
        ViewModel.Assets.Open(entry);
    }

    /// <summary>The View tab's exposure field, of the mood it shows now.</summary>
    public NumberFieldViewModel Exposure()
    {
        return ViewModel.ViewSettings.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Exposure");
    }

    /// <summary>Changes the file as another program would, then reports it as the content watcher does.</summary>
    public void ChangeOnDisk(string path, string text)
    {
        WriteFile(path, text);
        _content.NotifyChanged(path);
    }

    public string ReadFile(string path)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        return File.ReadAllText(fullPath);
    }

    public void Dispose()
    {
        _scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }

    private Task<UnsavedAssetsChoice?> Ask(IReadOnlyList<string> paths)
    {
        Prompts.Add(paths);
        return Task.FromResult(Answer);
    }

    public void WriteFile(string path, string contents)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        File.WriteAllText(fullPath, contents);
    }
}
