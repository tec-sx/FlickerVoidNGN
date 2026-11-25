using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

/// <summary>Builds a game with rendering, whose settings are under test, and a scene renderer on a Vulkan device for them to set, skipping without Vulkan 1.3.</summary>
internal sealed class RenderingSettingsTestsBuilder : IDisposable
{
    private readonly Game _game = new(NullLoggerFactory.Instance);
    private IGraphicsDevice? _device;
    private SceneRenderer? _renderer;

    public RenderingSettingsTestsBuilder()
    {
        _game.AddRendering();
    }

    public IDebugConsole Console => _game.DebugConsole;

    public RenderingSettings BuildSettings()
    {
        return _game.Services.GetService<RenderingSettings>()!;
    }

    public SceneRenderer BuildRenderer()
    {
        try
        {
            _device = new VulkanGraphicsDevice(new GraphicsDeviceOptions(), NullLoggerFactory.Instance);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
            throw;
        }

        ContentManager content = new(Path.GetTempPath());
        _renderer = new SceneRenderer(_device, content, PixelFormat.Rgba8UNorm);
        return _renderer;
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _device?.Dispose();
        _game.Dispose();
    }
}
