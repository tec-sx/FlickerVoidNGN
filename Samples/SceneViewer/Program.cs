using System.Globalization;
using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Shaders;
using FlickerVoid.Samples.SceneViewer;

using Microsoft.Extensions.Logging;

// Shows a glTF model (.glb, or .gltf with its files) through a camera that orbits it, lit by a sun, ambient light and
// optionally a ring of coloured point lights that cast shadows while the shadow atlas has room (eight of them).
// Usage: SceneViewer [model] [--validation] [--frames <count>] [--lights <count>] [--sun <x,y,z>] [--ambient <0..1>] [--fog <density>] [--probes].
// --probes bakes an irradiance volume fitted to the model at startup and saves it beside the model as <model>.probes. Without a model it shows Content/Room.glb.
string model = Path.Combine(AppContext.BaseDirectory, "Content", "Room.glb");
bool validation = false;
int frameLimit = 0;
int lightCount = 0;
Vector3? sunDirection = null;
bool bakeProbes = false;
float? ambient = null;
float fogDensity = 0;
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

if (!File.Exists(model))
{
    Console.Error.WriteLine($"{model} doesn't exist.");
    return 1;
}

string folder = Path.GetDirectoryName(model)!;
string fileName = Path.GetFileName(model);
ContentManager content = new(folder);

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
GraphicsDeviceOptions options = new() { Validation = validation };
using VulkanGraphicsDevice device = new(options, loggerFactory);
using DxcShaderCompiler compiler = new();
using Game game = new(loggerFactory);
game.AddRendering();
using GameWindow window = new(game, $"FlickerVoid  {fileName}  (drag to turn, wheel to zoom, WASD to move, P for perspective)", 1280, 720);

Entity camera = game.World.Create(new Transform(), new Camera());
game.World.Create(new Transform(), new MeshRenderer { Mesh = fileName });
Vector3[] palette = [new(1, 0.3f, 0.2f), new(1, 0.8f, 0.3f), new(0.3f, 1, 0.4f), new(0.3f, 0.8f, 1), new(0.4f, 0.4f, 1), new(1, 0.4f, 0.9f)];
for (int index = 0; index < lightCount; index++)
{
    float angle = MathF.Tau * index / lightCount;
    Vector3 position = new(MathF.Cos(angle) * 3, 1, MathF.Sin(angle) * 3);
    Transform transform = new() { Position = position };
    PointLight light = new() { Color = palette[index % palette.Length], Intensity = 3, Range = 3, CastsShadows = true };
    game.World.Create(transform, light);
}

OrbitCameraSystem orbit = new(window, game.World, camera, new Vector3(0, 0.5f, 0), distance: 10);
if (bakeProbes)
{
    ModelProbes.Bake(device, compiler, content, game.World, model, Configure);
}

SceneRenderer CreateRenderer(PixelFormat format)
{
    SceneRenderer sceneRenderer = new(device, compiler, content, format);
    Configure(sceneRenderer);
    return sceneRenderer;
}

void Configure(SceneRenderer sceneRenderer)
{
    if (sunDirection is { } direction)
    {
        sceneRenderer.SunDirection = direction;
    }

    if (ambient is { } share)
    {
        sceneRenderer.Ambient = share;
    }

    // Ground fog: full density up to the floor, thinning by e every 2/3 of a unit above it.
    sceneRenderer.FogDensity = fogDensity;
    sceneRenderer.FogHeightFalloff = 1.5f;
}

using SceneRenderSystem renderer = new(window, device, game.World, CreateRenderer, frameLimit);
game.FrameSystems.Add(orbit);
game.FrameSystems.Add(renderer);
window.Run();
Console.WriteLine($"Rendered {renderer.Frames} frames of {fileName} with Vulkan on {device.AdapterName}.");
return 0;
