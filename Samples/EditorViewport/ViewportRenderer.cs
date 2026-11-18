using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Vulkan;
using FlickerVoid.Engine.Shaders;
using FlickerVoid.Samples.TexturedMesh;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Samples.EditorViewport;

/// <summary>Renders the turning cube on the engine's Vulkan device into a shared texture the size of the viewport.</summary>
internal sealed class ViewportRenderer : IDisposable
{
    /// <summary>Avalonia creates its side of the shared image with all four uses, so ours must match for the memory to be compatible.</summary>
    private const TextureUsage SharedUsage = TextureUsage.RenderTarget | TextureUsage.CopySource | TextureUsage.CopyDestination | TextureUsage.ShaderRead;
    private const PixelFormat SharedFormat = PixelFormat.Rgba8UNorm;

    private readonly ILoggerFactory _loggerFactory;
    private readonly VulkanGraphicsDevice _device;
    private readonly DxcShaderCompiler _compiler = new();
    private readonly CubeRenderer _cube;
    private readonly ICommandList _commands;
    private ITexture? _depth;

    public ViewportRenderer(bool validation)
    {
        _loggerFactory = LoggerFactory.Create(static logging => logging.AddSimpleConsole());
        GraphicsDeviceOptions options = new() { Validation = validation };
        _device = new VulkanGraphicsDevice(options, _loggerFactory);
        _cube = new CubeRenderer(_device, _compiler, SharedFormat);
        _commands = _device.CreateCommandList();
    }

    public string AdapterName => _device.AdapterName;

    public VulkanSharedTexture? Target { get; private set; }

    /// <summary>Recreates the shared texture at the new size; returns false when it already has that size.</summary>
    public bool Resize(int width, int height)
    {
        if (Target is not null && Target.Texture.Description.Width == width && Target.Texture.Description.Height == height)
        {
            return false;
        }

        _device.WaitIdle();
        Target?.Dispose();
        _depth?.Dispose();
        TextureDescription description = new(width, height, SharedFormat, SharedUsage) { Name = "Viewport" };
        Target = _device.CreateSharedTexture(description);
        _depth = _cube.CreateDepth(width, height);
        return true;
    }

    /// <summary>Renders a frame into the shared texture and hands it over in the CopySource state; call <see cref="Resize"/> first.</summary>
    public void Render(float angle)
    {
        VulkanSharedTexture target = Target!;
        target.BeginFrame();
        _commands.Begin();
        _cube.Record(_commands, target.Texture, _depth!, angle);
        _commands.Barrier(target.Texture, ResourceState.CopySource);
        _commands.End();
        _device.Submit(_commands);
        target.EndFrame();
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        Target?.Dispose();
        _depth?.Dispose();
        _cube.Dispose();
        _compiler.Dispose();
        _device.Dispose();
        _loggerFactory.Dispose();
    }
}
