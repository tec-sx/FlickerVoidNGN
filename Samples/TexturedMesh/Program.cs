using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Direct3D12;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Shaders;
using FlickerVoid.Samples.TexturedMesh;

using Microsoft.Extensions.Logging;

// The stage 3 gate: the same textured mesh on Direct3D 12 and Vulkan, in a window.
// Options: "--api direct3d12" or "--api vulkan" (Direct3D 12 by default on Windows, Vulkan elsewhere),
// "--validation" turns on the API's validation layer, and "--frames <count>" closes the window after that many frames.
GraphicsApi api = GraphicsApi.Vulkan;
if (OperatingSystem.IsWindows())
{
    api = GraphicsApi.Direct3D12;
}

bool validation = false;
int frameLimit = 0;
for (int index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--api":
            index++;
            api = Enum.Parse<GraphicsApi>(args[index], ignoreCase: true);
            break;
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
using IGraphicsDevice device = CreateDevice(api, options, loggerFactory);
using DxcShaderCompiler compiler = new();
using Game game = new(loggerFactory);
using GameWindow window = new(game, $"FlickerVoid  {device.Api} on {device.AdapterName}", 1280, 720);
using MeshRenderSystem renderer = new(window, device, compiler, frameLimit);
game.FrameSystems.Add(renderer);
window.Run();
Console.WriteLine($"Rendered {renderer.Frames} frames with {device.Api} on {device.AdapterName}.");
return 0;

static IGraphicsDevice CreateDevice(GraphicsApi api, GraphicsDeviceOptions options, ILoggerFactory loggerFactory)
{
    switch (api)
    {
        case GraphicsApi.Direct3D12:
            return new D3D12GraphicsDevice(options, loggerFactory);
        case GraphicsApi.Vulkan:
            return new VulkanGraphicsDevice(options, loggerFactory);
        default:
            throw new ArgumentOutOfRangeException(nameof(api), api, null);
    }
}
