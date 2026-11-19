using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Viewports;

/// <summary>
/// The view of a <see cref="ViewportViewModel"/>: shows the engine's frames in Avalonia's compositor and passes the mouse and keys
/// over it to the view model's camera. Imports the renderer's shared texture and semaphores once per size, then renders a frame
/// each composition update once Avalonia has taken the previous one.
/// </summary>
public sealed partial class ViewportControl : Control
{
    private const int TransferSourceLayout = (int)Silk.NET.Vulkan.ImageLayout.TransferSrcOptimal;

    /// <summary>Avalonia reports a wheel notch as a delta of one.</summary>
    private const float NotchesPerDelta = 1;

    private readonly Stopwatch _clock = new();
    private readonly HashSet<Key> _keys = [];
    private CompositionSurfaceVisual? _visual;
    private CompositionDrawingSurface? _surface;
    private ICompositionGpuInterop? _interop;
    private ViewportRenderer? _renderer;
    private ICompositionImportedGpuImage? _image;
    private ICompositionImportedGpuSemaphore? _ready;
    private ICompositionImportedGpuSemaphore? _released;
    private Task _lastUpdate = Task.CompletedTask;
    private bool _updateQueued;
    private ViewportButtons _buttons;
    private bool _alt;
    private Point? _lastPointer;
    private Vector2 _mouseDelta;
    private float _wheel;

    public ViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    private static string ImageHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;

    private static string SemaphoreHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    private ViewportViewModel? ViewModel => DataContext as ViewportViewModel;

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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        e.Pointer.Capture(this);
        TrackPointer(e);
        PointerPoint point = e.GetCurrentPoint(this);
        bool leftClick = point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
        if (leftClick && !_alt)
        {
            ViewportViewModel? viewModel = ViewModel;
            PixelSize size = GetPixelSize();
            Vector2 pixel = ToPixels(point.Position);
            viewModel?.PointerPressed(pixel, size.Width, size.Height);
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        TrackPointer(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            ViewModel?.PointerReleased();
        }

        if (_buttons == ViewportButtons.None)
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TrackPointer(e);
        PointerPoint point = e.GetCurrentPoint(this);
        PixelSize size = GetPixelSize();
        Vector2 pixel = ToPixels(point.Position);
        ViewModel?.PointerMoved(pixel, size.Width, size.Height);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _buttons = ViewportButtons.None;
        _lastPointer = null;
        ViewModel?.PointerReleased();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _wheel += (float)e.Delta.Y * NotchesPerDelta;
        _alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _keys.Add(e.Key);
        _alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        ViewModel?.KeyPressed(e.Key, _buttons);

        // Alt alone would otherwise move focus to the window's access keys.
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _keys.Remove(e.Key);
        _alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        e.Handled = true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keys.Clear();
        _alt = false;
    }

    private double GetScaling() => this.GetPresentationSource()?.RenderScaling ?? 1;

    private PixelSize GetPixelSize()
    {
        double scaling = GetScaling();
        return PixelSize.FromSize(Bounds.Size, scaling);
    }

    private Vector2 ToPixels(Point position)
    {
        float scaling = (float)GetScaling();
        return new Vector2((float)position.X, (float)position.Y) * scaling;
    }

    private void TrackPointer(PointerEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(this);
        PointerPointProperties properties = point.Properties;
        ViewportButtons buttons = ViewportButtons.None;
        if (properties.IsLeftButtonPressed)
        {
            buttons |= ViewportButtons.Left;
        }

        if (properties.IsMiddleButtonPressed)
        {
            buttons |= ViewportButtons.Middle;
        }

        if (properties.IsRightButtonPressed)
        {
            buttons |= ViewportButtons.Right;
        }

        _buttons = buttons;
        _alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (_lastPointer is { } last)
        {
            Point moved = point.Position - last;
            _mouseDelta += new Vector2((float)moved.X, (float)moved.Y);
        }

        _lastPointer = point.Position;
    }

    private async void Initialize()
    {
        CompositionVisual self = ElementComposition.GetElementVisual(this)!;
        Compositor compositor = self.Compositor;
        _surface = compositor.CreateDrawingSurface();
        _visual = compositor.CreateSurfaceVisual();
        _visual.Surface = _surface;
        ElementComposition.SetElementChildVisual(this, _visual);

        ViewportViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        _interop = await compositor.TryGetCompositionGpuInterop();
        if (_interop is null)
        {
            viewModel.Status = "Avalonia's renderer offers no GPU interop: it needs its Vulkan backend on a GPU that exports memory and semaphores.";
            return;
        }

        bool supported = _interop.SupportedImageHandleTypes.Contains(ImageHandleType) && _interop.SupportedSemaphoreTypes.Contains(SemaphoreHandleType);
        if (!supported)
        {
            viewModel.Status = "Avalonia can't import Vulkan images and semaphores on this GPU.";
            return;
        }

        try
        {
            _renderer = new ViewportRenderer(viewModel.Scene.Game.LoggerFactory, viewModel.Scene.Content, viewModel.Validation);
        }
        catch (GraphicsUnavailableException exception)
        {
            viewModel.Status = exception.Message;
            return;
        }

        viewModel.Status = $"Vulkan on {_renderer.AdapterName}.";
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
            ViewModel?.Status = _lastUpdate.Exception.InnerException?.Message ?? "The viewport stopped.";
            _renderer.Dispose();
            _renderer = null;
            return;
        }

        _visual!.Size = new Avalonia.Vector(Bounds.Width, Bounds.Height);
        double scaling = GetScaling();
        PixelSize size = GetPixelSize();
        if (size.Width > 0 && size.Height > 0)
        {
            float seconds = (float)_clock.Elapsed.TotalSeconds;
            _clock.Restart();
            MoveCamera(seconds, size.Height, scaling);
            Render(size);
        }

        QueueNextFrame();
    }

    /// <summary>Hands the input gathered since the last frame to the view model, with the mouse in pixels.</summary>
    private void MoveCamera(float seconds, int viewportHeight, double scaling)
    {
        ViewportViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        Vector2 pixels = _mouseDelta * (float)scaling;
        ViewportInput input = new(_buttons, _alt, _keys, pixels, _wheel);
        viewModel.Update(input, seconds, viewportHeight);
        _mouseDelta = Vector2.Zero;
        _wheel = 0;
    }

    private void Render(PixelSize size)
    {
        ViewportRenderer renderer = _renderer!;
        if (renderer.Resize(size.Width, size.Height))
        {
            Import(renderer.Target!, size);
        }

        ViewportViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        renderer.ShowGrid = viewModel.IsGridVisible;
        renderer.Mood = viewModel.Mood;
        viewModel.BuildOverlay(renderer.Overlay, size.Height);
        Camera camera = viewModel.Camera.ToCamera();
        Transform transform = viewModel.Camera.ToTransform();
        renderer.Render(viewModel.Scene.Game.World, camera, transform);
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
