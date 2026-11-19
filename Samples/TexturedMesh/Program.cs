using FlickerVoid.Engine.Graphics.Vulkan.Devices;

using FlickerVoid.Engine.Graphics.Textures;

using FlickerVoid.Engine.Graphics.Devices;

using FlickerVoid.Engine.Platform.Windowing;

using FlickerVoid.Engine.Runtime.Loop;

using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Shaders;
using FlickerVoid.Samples.TexturedMesh;

using Microsoft.Extensions.Logging;

// The stage 3 gate: a textured mesh on Vulkan, in a window, on Windows and Linux.
// Options: "--validation" turns on the Vulkan validation layer, and "--frames <count>" closes the window after that many frames, and "--debug" shows the debug panels.
bool validation = false;
int frameLimit = 0;
bool showDebug = false;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--validation":
            validation = true;
            break;
        case "--debug":
            showDebug = true;
            break;
        case "--frames":
            index++;
            frameLimit = int.Parse(args[index]);
            break;
        default:
            Console.Error.WriteLine($"Unknown option {args[index]}.");
            return 1;
    }
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
GraphicsDeviceOptions options = new() { Validation = validation };
using VulkanGraphicsDevice device = new(options, loggerFactory);
using DxcShaderCompiler compiler = new();
using Game game = new(loggerFactory);
using GameWindow window = new(game, $"FlickerVoid  Vulkan on {device.AdapterName}", 1280, 720);
MeshWindow meshWindow = new();
DebugUi CreateDebugUi(PixelFormat format)
{
    string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlickerVoid", "TexturedMesh");
    DebugUi ui = new(device, compiler, window, format, settings);
    ui.AddEngineWindows(game, device);
    ui.Windows.Add(meshWindow);
    ui.LoadLayout();
    if (showDebug)
    {
        ui.Visible = true;
    }

    return ui;
}

using MeshRenderSystem renderer = new(window, device, compiler, frameLimit, meshWindow, CreateDebugUi);
game.FrameSystems.Add(renderer);
window.Run();
Console.WriteLine($"Rendered {renderer.Frames} frames with Vulkan on {device.AdapterName}.");
return 0;
