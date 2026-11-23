using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Overlays;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Editor.Viewports;

/// <summary>
/// Renders the edited world and the grid on the engine's Vulkan device into a shared texture the size of the viewport. The scene is
/// drawn into an sRGB texture and its bytes copied into the shared one, which Avalonia imports as plain UNorm and shows as it is.
/// A selected camera's view is drawn by a second renderer without the editor passes and copied into the bottom right corner, framed.
/// </summary>
internal sealed class ViewportRenderer : IDisposable
{
    /// <summary>Avalonia creates its side of the shared image with all four uses, so ours must match for the memory to be compatible.</summary>
    private const TextureUsage SharedUsage = TextureUsage.RenderTarget | TextureUsage.CopySource | TextureUsage.CopyDestination | TextureUsage.ShaderRead;
    private const PixelFormat SharedFormat = PixelFormat.Rgba8UNorm;
    private const PixelFormat ViewFormat = PixelFormat.Rgba8UNormSrgb;

    /// <summary>The preview's width as a share of the viewport's, and its distance from the corner in pixels.</summary>
    private const int PreviewShare = 4;
    private const int PreviewMargin = 12;
    private const int PreviewMinimumWidth = 64;
    private const int FrameWidth = 1;

    /// <summary>The editor's accent colour as sRGB bytes; the frame texture is UNorm, so its bytes land in the view as they are.</summary>
    private static readonly Vector4 FrameColor = new(0.847f, 0.584f, 0.353f, 1);

    private readonly VulkanGraphicsDevice _device;
    private readonly SceneRenderer _scene;
    private readonly GridPass _grid;
    private readonly OverlayPass _overlay;
    private readonly ICommandList _commands;
    private readonly IContentManager _content;
    private ITexture? _view;
    private SceneRenderer? _previewScene;
    private ITexture? _preview;
    private ITexture? _frame;

    public ViewportRenderer(ILoggerFactory loggerFactory, IContentManager content, bool validation)
    {
        GraphicsDeviceOptions options = new() { Validation = validation };
        _device = new VulkanGraphicsDevice(options, loggerFactory);
        _scene = new SceneRenderer(_device, content, ViewFormat) { Sky = true };
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Editor");
        _grid = new GridPass(_device, shaders, ViewFormat);
        _scene.Pipeline.Add(_grid);
        string icons = Path.Combine(AppContext.BaseDirectory, "Content", "Engine", "EditorIcons");
        _overlay = new OverlayPass(_device, shaders, ViewFormat, icons);
        _scene.Pipeline.Add(_overlay);
        _commands = _device.CreateCommandList();
        _content = content;
    }

    public string AdapterName => _device.AdapterName;

    public VulkanSharedTexture? Target { get; private set; }

    /// <summary>What the overlay pass draws over the scene each frame.</summary>
    public OverlayBuilder Overlay => _overlay.Overlay;

    /// <summary>The light and grade the scene is drawn with.</summary>
    public LightingMood Mood
    {
        get => _scene.Mood;
        set => _scene.Mood = value;
    }

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
        TextureDescription view = new(width, height, ViewFormat, TextureUsage.RenderTarget | TextureUsage.CopySource | TextureUsage.CopyDestination) { Name = "Viewport view" };
        _view = _device.CreateTexture(view);
        _preview?.Dispose();
        _preview = null;
        _frame?.Dispose();
        _frame = null;
        return true;
    }

    /// <summary>
    /// Renders a frame into the shared texture and hands it over in the CopySource state, with the preview camera's view in a corner
    /// when there is one; call <see cref="Resize"/> first.
    /// </summary>
    public void Render(World world, in Camera camera, in Transform cameraTransform, RenderCamera? preview)
    {
        VulkanSharedTexture target = Target!;
        ITexture view = _view!;
        target.BeginFrame();
        _commands.Begin();
        _scene.Render(_commands, world, view, camera, cameraTransform);
        if (preview is { } previewCamera)
        {
            RenderPreview(world, view, previewCamera);
        }

        _commands.Barrier(view, ResourceState.CopySource);
        _commands.Barrier(target.Texture, ResourceState.CopyDestination);
        _commands.CopyTexture(view, target.Texture);
        _commands.Barrier(target.Texture, ResourceState.CopySource);
        _commands.End();
        _device.Submit(_commands);
        target.EndFrame();
    }

    /// <summary>Draws the preview camera's view the viewport's shape, a quarter of its width, and copies it framed into the bottom right.</summary>
    private void RenderPreview(World world, ITexture view, in RenderCamera preview)
    {
        int viewWidth = view.Description.Width;
        int viewHeight = view.Description.Height;
        int width = viewWidth / PreviewShare;
        int height = width * viewHeight / viewWidth;
        if (width < PreviewMinimumWidth)
        {
            return;
        }

        if (_preview is null || _frame is null)
        {
            TextureDescription previewDescription = new(width, height, ViewFormat, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Camera preview" };
            _preview = _device.CreateTexture(previewDescription);
            int framed = FrameWidth * 2;
            TextureDescription frameDescription = new(width + framed, height + framed, PixelFormat.Rgba8UNorm, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = "Camera preview frame" };
            _frame = _device.CreateTexture(frameDescription);
        }

        _previewScene ??= new SceneRenderer(_device, _content, ViewFormat) { Sky = true };
        _previewScene.Mood = _scene.Mood;
        _previewScene.Render(_commands, world, _preview, preview.Camera, preview.Transform);
        _commands.Barrier(_frame, ResourceState.RenderTarget);
        ColorTarget frameClear = new(_frame, LoadAction.Clear, FrameColor);
        _commands.BeginRendering([frameClear]);
        _commands.EndRendering();

        int x = viewWidth - width - PreviewMargin;
        int y = viewHeight - height - PreviewMargin;
        _commands.Barrier(_frame, ResourceState.CopySource);
        _commands.Barrier(_preview, ResourceState.CopySource);
        _commands.Barrier(view, ResourceState.CopyDestination);
        _commands.CopyTexture(_frame, view, x - FrameWidth, y - FrameWidth);
        _commands.CopyTexture(_preview, view, x, y);
    }

    public void Dispose()
    {
        _device.WaitIdle();
        _commands.Dispose();
        _preview?.Dispose();
        _frame?.Dispose();
        _previewScene?.Dispose();
        Target?.Dispose();
        _view?.Dispose();
        _scene.Dispose();
        _device.Dispose();
    }
}
