using System.Diagnostics;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

using FlickerVoid.Engine.Graphics;
using FlickerVoid.Engine.Graphics.Vulkan;

namespace FlickerVoid.Samples.EditorViewport;

/// <summary>
/// Shows the engine's frames in Avalonia's compositor: imports the renderer's shared texture and semaphores once per size, then
/// renders a frame each composition update once Avalonia has taken the previous one.
/// </summary>
internal sealed partial class ViewportControl(bool validation, TextBlock status) : Control
{
    private const float RadiansPerSecond = 0.6f;
    private const int TransferSourceLayout = (int)Silk.NET.Vulkan.ImageLayout.TransferSrcOptimal;

    private readonly Stopwatch _clock = new();
    private CompositionSurfaceVisual? _visual;
    private CompositionDrawingSurface? _surface;
    private ICompositionGpuInterop? _interop;
    private ViewportRenderer? _renderer;
    private ICompositionImportedGpuImage? _image;
    private ICompositionImportedGpuSemaphore? _ready;
    private ICompositionImportedGpuSemaphore? _released;
    private Task _lastUpdate = Task.CompletedTask;
    private bool _updateQueued;

    private static string ImageHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;

    private static string SemaphoreHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Initialize();
    }

    protected override async void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ViewportRenderer? renderer = _renderer;
        _renderer = null;
        await _lastUpdate;
        await DisposeImportsAsync();
        _surface?.Dispose();
        renderer?.Dispose();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            QueueNextFrame();
        }
    }

    private async void Initialize()
    {
        CompositionVisual self = ElementComposition.GetElementVisual(this)!;
        Compositor compositor = self.Compositor;
        _surface = compositor.CreateDrawingSurface();
        _visual = compositor.CreateSurfaceVisual();
        _visual.Surface = _surface;
        ElementComposition.SetElementChildVisual(this, _visual);

        _interop = await compositor.TryGetCompositionGpuInterop();
        if (_interop is null)
        {
            status.Text = "Avalonia's renderer offers no GPU interop: it needs its Vulkan backend on a GPU that exports memory and semaphores, which lavapipe doesn't.";
            return;
        }

        bool supported = _interop.SupportedImageHandleTypes.Contains(ImageHandleType) && _interop.SupportedSemaphoreTypes.Contains(SemaphoreHandleType);
        if (!supported)
        {
            status.Text = "Avalonia can't import Vulkan images and semaphores on this GPU.";
            return;
        }

        try
        {
            _renderer = new ViewportRenderer(validation);
        }
        catch (GraphicsUnavailableException exception)
        {
            status.Text = exception.Message;
            return;
        }

        status.Text = $"Vulkan on {_renderer.AdapterName}, shown by Avalonia's compositor.";
        _clock.Start();
        QueueNextFrame();
    }

    private void QueueNextFrame()
    {
        if (_renderer is null || _updateQueued)
        {
            return;
        }

        _updateQueued = true;
        _visual!.Compositor.RequestCompositionUpdate(UpdateFrame);
    }

    private void UpdateFrame()
    {
        _updateQueued = false;
        if (_renderer is null)
        {
            return;
        }

        if (!_lastUpdate.IsCompleted)
        {
            QueueNextFrame();
            return;
        }

        if (_lastUpdate.IsFaulted)
        {
            status.Text = _lastUpdate.Exception.InnerException?.Message;
            _renderer.Dispose();
            _renderer = null;
            return;
        }

        _visual!.Size = new Vector(Bounds.Width, Bounds.Height);
        double scaling = this.GetPresentationSource()?.RenderScaling ?? 1;
        PixelSize size = PixelSize.FromSize(Bounds.Size, scaling);
        if (size.Width > 0 && size.Height > 0)
        {
            Render(size);
        }

        QueueNextFrame();
    }

    private void Render(PixelSize size)
    {
        ViewportRenderer renderer = _renderer!;
        if (renderer.Resize(size.Width, size.Height))
        {
            Import(renderer.Target!, size);
        }

        float angle = (float)_clock.Elapsed.TotalSeconds * RadiansPerSecond;
        renderer.Render(angle);
        _lastUpdate = _surface!.UpdateWithSemaphoresAsync(_image!, _ready!, _released!);
    }

    /// <summary>Imports the shared texture's memory and semaphores; Avalonia runs the import before any later update.</summary>
    private void Import(VulkanSharedTexture target, PixelSize size)
    {
        _ = DisposeImportsAsync();
        VulkanSharedHandles handles = target.Export();
        PlatformGraphicsExternalImageProperties properties = new()
        {
            Width = size.Width,
            Height = size.Height,
            Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
            MemorySize = (ulong)handles.MemorySize,
            VulkanProperties = new PlatformGraphicsExternalImageVulkanProperties { Layout = TransferSourceLayout },
        };
        ICompositionGpuInterop interop = _interop!;
        _image = interop.ImportImage(new PlatformHandle(handles.Memory, ImageHandleType), properties);
        _ready = interop.ImportSemaphore(new PlatformHandle(handles.Ready, SemaphoreHandleType));
        _released = interop.ImportSemaphore(new PlatformHandle(handles.Released, SemaphoreHandleType));
        if (OperatingSystem.IsWindows())
        {
            CloseAfterImport(_image.ImportCompleted, handles.Memory);
            CloseAfterImport(_ready.ImportCompleted, handles.Ready);
            CloseAfterImport(_released.ImportCompleted, handles.Released);
        }
    }

    private async Task DisposeImportsAsync()
    {
        ICompositionImportedGpuImage? image = _image;
        ICompositionImportedGpuSemaphore? ready = _ready;
        ICompositionImportedGpuSemaphore? released = _released;
        _image = null;
        _ready = null;
        _released = null;
        if (image is not null)
        {
            await image.DisposeAsync();
        }

        if (ready is not null)
        {
            await ready.DisposeAsync();
        }

        if (released is not null)
        {
            await released.DisposeAsync();
        }
    }

    /// <summary>Importing an NT handle doesn't take it over the way importing a file descriptor does, so we close ours once Avalonia has its own.</summary>
    private static async void CloseAfterImport(Task import, nint handle)
    {
        try
        {
            await import;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
