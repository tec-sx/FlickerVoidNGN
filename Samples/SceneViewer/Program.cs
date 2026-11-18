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

// Shows a glTF model (.glb, or .gltf with its files) through a camera that orbits it, lit by a sun and ambient light.
// Usage: SceneViewer [model] [--validation] [--frames <count>]. Without a model it shows Content/Room.glb.
string model = Path.Combine(AppContext.BaseDirectory, "Content", "Room.glb");
bool validation = false;
int frameLimit = 0;
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
OrbitCameraSystem orbit = new(window, game.World, camera, new Vector3(0, 0.5f, 0), distance: 10);
using SceneRenderSystem renderer = new(window, device, game.World, format => new SceneRenderer(device, compiler, content, format), frameLimit);
game.FrameSystems.Add(orbit);
game.FrameSystems.Add(renderer);
window.Run();
Console.WriteLine($"Rendered {renderer.Frames} frames of {fileName} with Vulkan on {device.AdapterName}.");
return 0;
