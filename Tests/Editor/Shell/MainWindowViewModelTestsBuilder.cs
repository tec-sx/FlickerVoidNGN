using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Persistence.Layout;
using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;
using FlickerVoid.Editor.Viewports;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests;

/// <summary>
/// Builds the main window's view model over a temporary content folder holding Room.fvscene and Hall.fvscene, each with a Lamp,
/// Paper.fvmat, Night.fvmood, whose exposure is 0.5, Lantern.fvdata, a lantern of 6 hours, Cellar.fvflow, a flow with a start node, and Lit.fvscene, which names Night.fvmood as its mood; the user's answer to the unsaved files prompt is <see cref="Answer"/>, and playing starts the substitute <see cref="Launcher"/>.
/// <see cref="OpenStreet"/> opens a street with a van of a car file at the origin and a crate, seen in a 640x480 viewport.
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
    public const string Street = "Street.fvscene";
    public const string Van = "Vans/Van.fvvehicle";
    public const string House = "Houses/House.fvbuilding";
    public const string Program = "/game/Game.dll";
    public const int Width = 640;
    public const int Height = 480;

    private const string StreetScene = """
        { "entities": [
          { "name": "Van", "components": { "transform": {}, "vehicle": { "file": "Vans/Van.fvvehicle" } } },
          { "name": "Crate", "components": { "transform": { "position": [-4, 0, 0] }, "greybox": { "size": [1, 1, 1] } } }
        ] }
        """;

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
        LayoutFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Layout.json");
        EditorLayout layout = new(LayoutFile);
        ScenePlayer player = new(_scene, Program, Launcher);
        ViewModel = new MainWindowViewModel(viewport, inspector, _content, player, layout: layout);
        ViewModel.AskUnsaved = Ask;
    }

    public MainWindowViewModel ViewModel { get; }

    /// <summary>Starts the game when the view model plays the scene.</summary>
    public IGameLauncher Launcher { get; } = Substitute.For<IGameLauncher>();

    /// <summary>The arguments of the last time the game was started.</summary>
    public IReadOnlyList<string> PlayedArguments()
    {
        NSubstitute.Core.ICall call = Launcher.ReceivedCalls().Last();
        return (IReadOnlyList<string>)call.GetArguments()[1]!;
    }

    /// <summary>The editor layout's file, in a temporary folder of its own.</summary>
    public string LayoutFile { get; }

    public EditorScene Scene => _scene;

    /// <summary>What the prompt answers; null cancels.</summary>
    public UnsavedAssetsChoice? Answer { get; set; } = UnsavedAssetsChoice.Save;

    /// <summary>The files each prompt listed, one list per prompt.</summary>
    public List<IReadOnlyList<string>> Prompts { get; } = [];

    /// <summary>Writes the car file and the street, lists them in the asset browser, opens the street from there and looks at the van.</summary>
    public void OpenStreet()
    {
        string folder = Path.Combine(_contentFolder, "Vans");
        Directory.CreateDirectory(folder);
        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        string json = JsonSerializer.Serialize(car, VehiclesJsonContext.Default.VehicleFile);
        WriteFile(Van, json);
        WriteFile(Street, StreetScene);
        ViewModel.Assets.Refresh();
        OpenInBrowser(Street);
        ViewModel.Viewport.Camera.LookAt(Vector3.Zero, yaw: 0.6f, pitch: -0.4f, distance: 8);
    }

    public Entity Find(string name) => _scene.Find(name)!.Value;

    /// <summary>The middle of the entity's box, its parts' for a creation, where a click lands on it.</summary>
    public Vector3 MiddleOf(string name)
    {
        Entity entity = Find(name);
        World world = _scene.Game.World;
        _scene.Picker.TryGetBounds(world, entity, out Vector3 low, out Vector3 high).Should().BeTrue();
        Matrix4x4 matrix = world.Get<Engine.Runtime.Components.Transform>(entity).ToMatrix();
        Vector3 middle = (low + high) / 2;
        return Vector3.Transform(middle, matrix);
    }

    /// <summary>Double-clicks the viewport where the point shows: a press, a release, then the second press.</summary>
    public void DoubleClick(Vector3 point)
    {
        ViewportViewModel viewport = ViewModel.Viewport;
        Vector2 pixel = viewport.Camera.Project(point, Width, Height);
        viewport.PointerPressed(pixel, Width, Height);
        viewport.PointerReleased();
        viewport.DoubleClicked(pixel, Width, Height);
    }

    /// <summary>Double-clicks the viewport's top left corner, where the sky shows.</summary>
    public void DoubleClickSky()
    {
        ViewportViewModel viewport = ViewModel.Viewport;
        Vector2 corner = new(2, 2);
        viewport.PointerPressed(corner, Width, Height);
        viewport.PointerReleased();
        viewport.DoubleClicked(corner, Width, Height);
    }

    /// <summary>The wheel count the file holds as saved on disk.</summary>
    /// <summary>Writes the vehicle's look as the content build bakes it, one triangle in an object named by the node, and reports it changed.</summary>
    public void WriteLook(string vehiclePath, string node)
    {
        MeshVertex[] vertices = [new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero), new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX), new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY)];
        MaterialData material = new(Vector4.One, null);
        MeshPart part = new(vertices, [0, 2, 1], material) { Nodes = [node] };
        ModelData look = new([part], []);
        string path = VehicleLook.GetBuiltPath(vehiclePath);
        string fullPath = _scene.Content.GetFullPath(path);
        using (FileStream stream = File.Create(fullPath))
        {
            ModelFile.Write(look, stream);
        }

        _scene.Content.NotifyChanged(path);
    }

    public int WheelsOnDisk(string path)
    {
        string json = ReadFile(path);
        VehicleFile vehicle = JsonSerializer.Deserialize(json, VehiclesJsonContext.Default.VehicleFile)!;
        return vehicle.Wheels.Count;
    }

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

    /// <summary>Writes a new building's file and lists it in the asset browser.</summary>
    public void WriteHouse()
    {
        string folder = Path.Combine(_contentFolder, "Houses");
        Directory.CreateDirectory(folder);
        BuildingFile house = BuildingFile.Create();
        string json = JsonSerializer.Serialize(house, BuildingsJsonContext.Default.BuildingFile);
        WriteFile(House, json);
        ViewModel.Assets.Refresh();
    }

    /// <summary>Opens the file as the asset browser would, as a scene unless another type is given.</summary>
    public void OpenInBrowser(string path, AssetType? type = null)
    {
        AssetEntry entry = new(path, Path.GetFileNameWithoutExtension(path), type ?? AssetTypes.Scene);
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
        string layoutFolder = Path.GetDirectoryName(LayoutFile)!;
        if (Directory.Exists(layoutFolder))
        {
            Directory.Delete(layoutFolder, recursive: true);
        }
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
