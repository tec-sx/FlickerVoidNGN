using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Shaders;
using FlickerVoid.Samples.TexturedMesh;

using Microsoft.Extensions.Logging;

// The stage 3 gate: a textured mesh on Vulkan, in a window, on Windows and Linux.
// Options: "--validation" turns on the Vulkan validation layer, and "--frames <count>" closes the window after that many frames.
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
using MeshRenderSystem renderer = new(window, device, compiler, frameLimit);
game.FrameSystems.Add(renderer);
window.Run();
Console.WriteLine($"Rendered {renderer.Frames} frames with Vulkan on {device.AdapterName}.");
return 0;
