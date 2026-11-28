using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using Arch.Core;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Controls;
using FlickerVoid.Editor.Core.Cameras;
using FlickerVoid.Editor.Core.Menus;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Viewports;

/// <summary>
/// The view of a <see cref="ViewportViewModel"/>: shows the engine's frames in Avalonia's compositor and passes the mouse and keys
/// over it to the view model's camera. Imports the renderer's shared texture and semaphores once per size, then renders a frame
/// each composition update once Avalonia has taken the previous one.
/// </summary>
public sealed partial class ViewportControl : Control, ICustomHitTest
{
    private const int TransferSourceLayout = (int)Silk.NET.Vulkan.ImageLayout.TransferSrcOptimal;

    /// <summary>Avalonia reports a wheel notch as a delta of one.</summary>
    private const float NotchesPerDelta = 1;

    /// <summary>How far the mouse may move, in pixels, for a right-click to open the options menu rather than look round.</summary>
    private const double ClickSlop = 4;

    private readonly Stopwatch _clock = new();
    private readonly HashSet<ViewportKey> _keys = [];
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
    private Point? _rightPressedAt;
    private Vector2 _mouseDelta;
    private float _wheel;

    public ViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, HandleDragOver);
        AddHandler(DragDrop.DropEvent, HandleDrop);
    }

    private static string ImageHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;

    private static string SemaphoreHandleType => OperatingSystem.IsWindows()
        ? KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaqueNtHandle
        : KnownPlatformGraphicsExternalSemaphoreHandleTypes.VulkanOpaquePosixFileDescriptor;

    private ViewportViewModel? ViewModel => DataContext as ViewportViewModel;

    public bool HitTest(Point point)
    {
        Rect bounds = new(Bounds.Size);
        return bounds.Contains(point);
    }

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
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            _rightPressedAt = point.Position;
        }

        if (leftClick && !_alt)
        {
            ViewportViewModel? viewModel = ViewModel;
            PixelSize size = GetPixelSize();
            Vector2 pixel = ToPixels(point.Position);
            if (e.ClickCount == 2)
            {
                viewModel?.DoubleClicked(pixel, size.Width, size.Height, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
            else
            {
                viewModel?.PointerPressed(pixel, size.Width, size.Height, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control));
            }
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

        Point released = e.GetPosition(this);
        if (e.InitialPressMouseButton == MouseButton.Right && _rightPressedAt is { } pressed && !_alt)
        {
            Point moved = released - pressed;
            if (Math.Abs(moved.X) <= ClickSlop && Math.Abs(moved.Y) <= ClickSlop)
            {
                ShowOptions(released);
            }
        }

        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _rightPressedAt = null;
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

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ViewModel?.PointerLeft();
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
        _alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (ViewportKeys.From(e.Key) is { } key)
        {
            _keys.Add(key);
            ViewModel?.KeyPressed(key, _buttons, e.KeyModifiers.HasFlag(KeyModifiers.Control));
        }

        // Alt alone would otherwise move focus to the window's access keys; Ctrl shortcuts (undo, save) go on to the window.
        e.Handled = !e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (ViewportKeys.From(e.Key) is { } key)
        {
            _keys.Remove(key);
        }

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

    /// <summary>A right-click that didn't look round: selects what is under the mouse and offers what to do with it.</summary>
    private void ShowOptions(Point position)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        PixelSize size = GetPixelSize();
        Vector2 pixel = ToPixels(position);
        _keys.Clear();
        if (viewModel.MeshTools.CancelDrag())
        {
            return;
        }

        if (viewModel.MeshTools.IsMeshMode)
        {
            List<MenuChoice> tools = [.. viewModel.MeshTools.GetMenu()];
            AddUndoChoices(tools, viewModel.Mode.History);
            EditorMenus.ShowOptions(this, tools, MenuGrouping.Separators);
            return;
        }

        if (viewModel.IsCreating)
        {
            ShowCreatorOptions(viewModel);
            return;
        }

        viewModel.SelectAt(pixel, size.Width, size.Height);
        MenuChoice add = EditorMenus.AddEntry(this);
        MenuChoice duplicate = new("Duplicate", viewModel.DuplicateSelected, "Entity", "Duplicate", "Ctrl+D");
        MenuChoice delete = new("Delete", viewModel.DeleteSelected, "Entity", "Delete", "Delete");
        MenuChoice frame = new("Frame Selected", viewModel.FrameSelected, "View", "Search", "F");
        List<MenuChoice> choices = [add, frame];
        if (viewModel.Selection.Selected is not null)
        {
            choices.Add(duplicate);
            choices.Add(delete);
        }

        AddUndoChoices(choices, viewModel.Scene.History);
        EditorMenus.ShowOptions(this, choices, MenuGrouping.Separators);
    }

    /// <summary>The creator's options: frame the creation, undo and redo its file, and leave.</summary>
    private void ShowCreatorOptions(ViewportViewModel viewModel)
    {
        MenuChoice frame = new("Frame Creation", viewModel.FrameSelected, "View", "Search", "F");
        MenuChoice leave = new("Leave Creator", viewModel.LeaveCreator, "View", "Back", "Escape");
        List<MenuChoice> choices = [frame, leave];
        AddUndoChoices(choices, viewModel.Mode.History);
        EditorMenus.ShowOptions(this, choices, MenuGrouping.Separators);
    }

    private static void AddUndoChoices(List<MenuChoice> choices, UndoHistory history)
    {
        if (history.UndoName is { } undoName)
        {
            choices.Add(new MenuChoice($"Undo {undoName}", () => history.Undo(), "Edit", "Undo", "Ctrl+Z"));
        }

        if (history.RedoName is { } redoName)
        {
            choices.Add(new MenuChoice($"Redo {redoName}", () => history.Redo(), "Edit", "Redo", "Ctrl+Y"));
        }
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        bool placeable = AssetDrag.GetPlaceable(e.DataTransfer) is not null;
        e.DragEffects = placeable ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        string? asset = AssetDrag.GetPlaceable(e.DataTransfer);
        if (asset is null || ViewModel is not { } viewModel)
        {
            return;
        }

        Point position = e.GetPosition(this);
        PixelSize size = GetPixelSize();
        Vector2 pixel = ToPixels(position);
        viewModel.DropAsset(asset, pixel, size.Width, size.Height);
        Focus();
        e.Handled = true;
    }

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
            RenderingSettings settings = viewModel.Scene.Game.Services.GetService<RenderingSettings>()!;
            _renderer = new ViewportRenderer(viewModel.Scene.Game.LoggerFactory, viewModel.Scene.Content, viewModel.Validation, settings);
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
            ViewModel?.UpdateBuilding(seconds, size.Width, size.Height, scaling);
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
        renderer.FlatLook = viewModel.IsFlatLook;
        renderer.Mood = viewModel.GetDrawnMood();
        renderer.Meshes = viewModel.Scene.Meshes;
        viewModel.BuildOverlay(renderer.Overlay, size.Height);
        RenderCamera drawn = viewModel.GetDrawnCamera();
        Camera camera = drawn.Camera;
        Transform transform = drawn.Transform;
        RenderCamera? preview = viewModel.GetPreviewCamera();
        DepthFade viewFade = viewModel.GetDepthFade();
        DepthFade previewFade = viewModel.GetPreviewDepthFade();
        Vector3 focus = viewModel.GetDepthFocus();
        ViewportDepthFade depthFade = new(viewFade, previewFade, focus);
        RenderingSettings rendering = viewModel.Scene.Game.Services.GetService<RenderingSettings>()!;
        World world = viewModel.GetDrawnWorld();
        renderer.Render(world, rendering, camera, transform, preview, depthFade);
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
            TopLeftOrigin = true,
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
