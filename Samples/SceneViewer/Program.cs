using FlickerVoid.Engine.Graphics.Vulkan.Devices;

using FlickerVoid.Engine.Graphics.Textures;

using FlickerVoid.Engine.Rendering.Meshes;

using FlickerVoid.Engine.Rendering.Lighting;

using FlickerVoid.Engine.Graphics.Devices;

using FlickerVoid.Engine.Platform.Windowing;

using FlickerVoid.Engine.Runtime.Loop;

using FlickerVoid.Engine.Rendering.Cameras;

using System.Globalization;
using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Samples.SceneViewer;

using Microsoft.Extensions.Logging;

// Shows a glTF model (.glb, or .gltf with its files) through a camera that orbits it, lit by a sun, ambient light and
// optionally a ring of coloured point lights that cast shadows while the shadow atlas has room (eight of them).
// Usage: SceneViewer [model] [--validation] [--frames <count>] [--lights <count>] [--sun <x,y,z>] [--ambient <0..1>] [--fog <density>] [--probes]
//        [--moods <file,file,...>] [--scene <file>] [--debug].
// --scene spawns a scene file from the model's folder instead of the model, and walks its animated entities round the
// origin; Content/Scenes/PaintedRoom.fvscene is the stage 4 gate: the room, a walking fox and 36 lamps.
// F1 shows the debug interface (engine stats, console, rendering values, lights, and the viewer's own panel); --debug starts with it shown.
// --moods takes lighting mood files in the model's folder (Content/Moods/Day.fvmood, Dusk.fvmood and Night.fvmood beside the
// default room); N steps a fact through them, which mood rules turn into a blend to the next mood. --sun, --ambient and --fog override the mood.
// --probes bakes an irradiance volume fitted to the model at startup and saves it beside the model as <model>.fvprobes. Without a model it shows Content/Room.glb.
// Content reloads while the viewer runs: save a model, texture, scene or mood in Samples/SceneViewer/Content and the builder
// rebuilds it beside the build, where it shows within a second. A model passed by path must be in a built content folder.
string model = Path.Combine(AppContext.BaseDirectory, "Content", "Room.glb");
bool validation = false;
int frameLimit = 0;
int lightCount = 0;
Vector3? sunDirection = null;
bool bakeProbes = false;
float? ambient = null;
float? fogDensity = null;
string[] moodFiles = [];
string? sceneFile = null;
bool showDebug = false;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--validation":
            validation = true;
            break;
        case "--frames":
            index++;
            frameLimit = int.Parse(args[index]);
            break;
        case "--lights":
            index++;
            lightCount = int.Parse(args[index]);
            break;
        case "--ambient":
            index++;
            ambient = float.Parse(args[index], CultureInfo.InvariantCulture);
            break;
        case "--fog":
            index++;
            fogDensity = float.Parse(args[index], CultureInfo.InvariantCulture);
            break;
        case "--moods":
            index++;
            moodFiles = args[index].Split(',');
            break;
        case "--debug":
            showDebug = true;
            break;
        case "--scene":
            index++;
            sceneFile = args[index];
            break;
        case "--probes":
            bakeProbes = true;
            break;
        case "--sun":
            index++;
            float[] sun = [.. args[index].Split(',').Select(static part => float.Parse(part, CultureInfo.InvariantCulture))];
            Vector3 travel = new(sun[0], sun[1], sun[2]);
            sunDirection = Vector3.Normalize(travel);
            break;
        default:
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown option {args[index]}.");
                return 1;
            }

            model = Path.GetFullPath(args[index]);
            break;
    }
}

// The build bakes Room.glb into Room.fvmodel; content still names the model by its source.
string bakedModel = Path.ChangeExtension(model, ".fvmodel");
if (!File.Exists(model) && !File.Exists(bakedModel))
{
    Console.Error.WriteLine($"{model} doesn't exist.");
    return 1;
}

string folder = Path.GetDirectoryName(model)!;
string fileName = Path.GetFileName(model);
string shown = fileName;
if (sceneFile is not null)
{
    shown = Path.GetFileName(sceneFile);
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
HostOptions hostOptions = new() { Scene = sceneFile, ContentFolder = folder, FrameLimit = frameLimit, Validation = validation };
string title = $"FlickerVoid  {shown}  (drag to turn, wheel to zoom, WASD to move, P for perspective, N for the next mood)";
using SceneHost host = new(hostOptions, loggerFactory, title);
Game game = host.Game;
GameWindow window = host.Window;
ContentManager content = host.Content;

Entity camera = game.World.Create(new Transform(), new Camera());
if (sceneFile is null)
{
    game.World.Create(new Transform(), new MeshRenderer { Mesh = fileName });
}

Vector3[] palette = [new(1, 0.3f, 0.2f), new(1, 0.8f, 0.3f), new(0.3f, 1, 0.4f), new(0.3f, 0.8f, 1), new(0.4f, 0.4f, 1), new(1, 0.4f, 0.9f)];
for (int index = 0; index < lightCount; index++)
{
    float angle = MathF.Tau * index / lightCount;
    Vector3 position = new(MathF.Cos(angle) * 3, 1, MathF.Sin(angle) * 3);
    Transform transform = new() { Position = position };
    PointLight light = new() { Color = palette[index % palette.Length], Intensity = 3, Range = 3, CastsShadows = true };
    game.World.Create(transform, light);
}

DebugUi? debugUi = null;
bool MouseBlocked() => debugUi?.WantsMouse ?? false;
bool KeyboardBlocked() => debugUi?.WantsKeyboard ?? false;
OrbitCameraSystem orbit = new(window, game.World, camera, new Vector3(0, 0.5f, 0), distance: 10, MouseBlocked, KeyboardBlocked);
CircleWalkSystem walkers = new(game.World, speed: 0.5f);

// The first mood file is the default; each later one has a rule that holds while the viewer's mood fact is its index.
FactDatabase facts = new(new FactSettings());
List<MoodRule> rules = [];
for (int index = 1; index < moodFiles.Length; index++)
{
    FactCondition isThisMood = new() { Fact = MoodSystem.MoodFact, Compare = FactCompare.Equal, Value = index };
    ConditionSet when = new() { Conditions = [isThisMood] };
    rules.Add(new MoodRule { When = when, Mood = moodFiles[index] });
}

using LightingMoodDirector? director = CreateDirector();
LightingMood startMood = director?.Current ?? new LightingMood();
LightingMood configuredMood = Configure(startMood);
host.Mood = configuredMood;
SceneRenderSystem renderer = host.Start(CreateDebugUi);
if (bakeProbes)
{
    ModelProbes.Bake(host.Device, content, game.World, model, configuredMood);
}

LightingMoodDirector? CreateDirector()
{
    if (moodFiles.Length == 0)
    {
        return null;
    }

    ILogger<LightingMoodDirector> directorLogger = loggerFactory.CreateLogger<LightingMoodDirector>();
    return new LightingMoodDirector(facts, content, moodFiles[0], rules, directorLogger);
}

LightingMood Configure(LightingMood mood)
{
    if (sunDirection is { } direction)
    {
        mood = mood with { SunDirection = direction };
    }

    if (ambient is { } share)
    {
        mood = mood with { Ambient = share };
    }

    if (fogDensity is { } density)
    {
        // Ground fog: full density up to the floor, thinning by e every 2/3 of a unit above it.
        mood = mood with { FogDensity = density, FogHeightFalloff = 1.5f };
    }

    return mood;
}

DebugUi CreateDebugUi(PixelFormat format, SceneRenderer sceneRenderer)
{
    string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickerVoid", "SceneViewer");
    DebugUi ui = new(host.Device, window, format, settings);
    ui.AddEngineWindows(game, host.Device);
    ui.AddRenderingWindows(sceneRenderer, game.World);
    ViewerWindow viewer = new(game.World, camera, walkers, facts, moodFiles);
    ui.Windows.Add(viewer);
    ui.LoadLayout();
    if (showDebug)
    {
        ui.Visible = true;
    }

    debugUi = ui;
    return ui;
}

game.FrameSystems.Add(orbit);
game.FrameSystems.Add(walkers);
if (director is not null)
{
    MoodSystem moods = new(window, facts, director, renderer, Configure, moodFiles.Length, KeyboardBlocked);
    game.FrameSystems.Add(moods);
}

host.Run();
double milliseconds = renderer.AverageFrameTime.TotalMilliseconds;
double framesPerSecond = 0;
if (milliseconds > 0)
{
    framesPerSecond = 1000 / milliseconds;
}

Console.WriteLine($"Rendered {renderer.Frames} frames of {shown} with Vulkan on {host.Device.AdapterName}: {milliseconds:0.00} ms a frame ({framesPerSecond:0} fps).");
return 0;
