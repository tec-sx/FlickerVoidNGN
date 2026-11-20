using Arch.Core;

using FlickerVoid.Engine.Editor.Overlays;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// Renders the edited world and the grid on the engine's Vulkan device into a shared texture the size of the viewport. The scene is
/// drawn into an sRGB texture and its bytes copied into the shared one, which Avalonia imports as plain UNorm and shows as it is.
/// </summary>
internal sealed class ViewportRenderer : IDisposable
{
    /// <summary>Avalonia creates its side of the shared image with all four uses, so ours must match for the memory to be compatible.</summary>
    private const TextureUsage SharedUsage = TextureUsage.RenderTarget | TextureUsage.CopySource | TextureUsage.CopyDestination | TextureUsage.ShaderRead;
    private const PixelFormat SharedFormat = PixelFormat.Rgba8UNorm;
    private const PixelFormat ViewFormat = PixelFormat.Rgba8UNormSrgb;

    private readonly VulkanGraphicsDevice _device;
    private readonly SceneRenderer _scene;
    private readonly GridPass _grid;
    private readonly OverlayPass _overlay;
    private readonly ICommandList _commands;
    private ITexture? _view;

    public ViewportRenderer(ILoggerFactory loggerFactory, IContentManager content, bool validation)
    {
        GraphicsDeviceOptions options = new() { Validation = validation };
        _device = new VulkanGraphicsDevice(options, loggerFactory);
        _scene = new SceneRenderer(_device, content, ViewFormat);
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Editor");
        _grid = new GridPass(_device, shaders, ViewFormat);
        _scene.Pipeline.Add(_grid);
        string icons = Path.Combine(AppContext.BaseDirectory, "Content", "Engine", "EditorIcons");
        _overlay = new OverlayPass(_device, shaders, ViewFormat, icons);
        _scene.Pipeline.Add(_overlay);
        _commands = _device.CreateCommandList();
    }

    public string AdapterName => _device.AdapterName;

    public VulkanSharedTexture? Target { get; private set; }

    /// <summary>What the overlay pass draws over the scene each frame.</summary>
    public OverlayBuilder Overlay => _overlay.Overlay;

    public bool ShowGrid
    {
        get => _grid.Visible;
        set => _grid.Visible = value;
    }

    /// <summary>Recreates the textures at the new size; returns false when they already have that size.</summary>
    public bool Resize(int width, int height)
    {
        if (Target is not null && Target.Texture.Description.Width == width && Target.Texture.Description.Height == height)
        {
            return false;
        }

        _device.WaitIdle();
        Target?.Dispose();
        _view?.Dispose();
        TextureDescription shared = new(width, height, SharedFormat, SharedUsage) { Name = "Viewport" };
        Target = _device.CreateSharedTexture(shared);
        TextureDescription view = new(width, height, ViewFormat, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Viewport view" };
        _view = _device.CreateTexture(view);
        return true;
    }

    /// <summary>Renders a frame into the shared texture and hands it over in the CopySource state; call <see cref="Resize"/> first.</summary>
    public void Render(World world, in Camera camera, in Transform cameraTransform)
    {
        VulkanSharedTexture target = Target!;
        ITexture view = _view!;
        target.BeginFrame();
        _commands.Begin();
        _scene.Render(_commands, world, view, camera, cameraTransform);
        _commands.Barrier(view, ResourceState.CopySource);
        _commands.Barrier(target.Texture, ResourceState.CopyDestination);
        _commands.CopyTexture(view, target.Texture);
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
        _view?.Dispose();
        _scene.Dispose();
        _device.Dispose();
    }
}
