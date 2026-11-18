using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// A Vulkan swapchain with FIFO presentation. Each acquired image's semaphore is waited on by an empty submission that
/// later submissions wait for on the device's timeline; each image has its own semaphore that tells presentation the GPU is done.
/// </summary>
internal sealed unsafe class VulkanSwapchain : ISwapchain
{
    private const uint PreferredImageCount = 3;
    private const Silk.NET.Vulkan.Format SurfaceFormat = Silk.NET.Vulkan.Format.B8G8R8A8Unorm;

    private static readonly CompositeAlphaFlagsKHR[] CompositeAlphaPreference =
    [
        CompositeAlphaFlagsKHR.OpaqueBitKhr,
        CompositeAlphaFlagsKHR.InheritBitKhr,
        CompositeAlphaFlagsKHR.PreMultipliedBitKhr,
        CompositeAlphaFlagsKHR.PostMultipliedBitKhr,
    ];

    private readonly VulkanGraphicsDevice _device;
    private readonly KhrSurface _surfaceApi;
    private readonly KhrSwapchain _swapchainApi;
    private readonly SurfaceKHR _surface;
    private readonly string? _name;
    private SwapchainKHR _handle;
    private VulkanTexture[] _textures = [];
    private Silk.NET.Vulkan.Semaphore[] _acquireSemaphores = [];
    private ulong[] _acquireReleaseValues = [];
    private Silk.NET.Vulkan.Semaphore[] _presentSemaphores = [];
    private int _frame;
    private uint _imageIndex;
    private bool _acquired;
    private bool _outOfDate;
    private bool _disposed;

    public VulkanSwapchain(VulkanGraphicsDevice device, in SwapchainDescription description)
    {
        if (!device.CanPresent)
        {
            throw new GraphicsUnavailableException("The Vulkan driver has no window surface or swapchain extension.");
        }

        _device = device;
        _name = description.Name;
        Vk vk = device.Vk;
        if (!vk.TryGetInstanceExtension(device.Instance, out _surfaceApi) || !vk.TryGetDeviceExtension(device.Instance, device.Handle, out _swapchainApi))
        {
            throw new GraphicsUnavailableException("The Vulkan surface and swapchain functions could not be loaded.");
        }

        _surface = CreateSurface(description.Window);
        Bool32 supported = false;
        VulkanResult.Check(_surfaceApi.GetPhysicalDeviceSurfaceSupport(device.PhysicalDevice, device.QueueFamily, _surface, &supported));
        if (!supported)
        {
            _surfaceApi.DestroySurface(device.Instance, _surface, null);
            throw new GraphicsUnavailableException("The Vulkan graphics queue can't present to this window.");
        }

        Create(description.Width, description.Height);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public PixelFormat Format => PixelFormat.Bgra8UNorm;

    public ITexture AcquireNextTexture()
    {
        if (_outOfDate)
        {
            Recreate(Width, Height);
        }

        int slot = _frame % _acquireSemaphores.Length;
        _device.WaitFor(_acquireReleaseValues[slot]);
        Silk.NET.Vulkan.Semaphore acquired = _acquireSemaphores[slot];
        uint index = 0;
        Result result = _swapchainApi.AcquireNextImage(_device.Handle, _handle, ulong.MaxValue, acquired, default, &index);
        if (result == Result.ErrorOutOfDateKhr)
        {
            Recreate(Width, Height);
            slot = 0;
            acquired = _acquireSemaphores[slot];
            result = _swapchainApi.AcquireNextImage(_device.Handle, _handle, ulong.MaxValue, acquired, default, &index);
        }

        if (result != Result.SuboptimalKhr)
        {
            VulkanResult.Check(result);
        }

        _acquireReleaseValues[slot] = _device.WaitOnGpu(acquired);
        _frame = slot + 1;
        _imageIndex = index;
        _acquired = true;
        VulkanTexture texture = _textures[index];
        texture.State = ResourceState.Undefined;
        return texture;
    }

    public void Present()
    {
        if (!_acquired)
        {
            throw new InvalidOperationException("Acquire a back buffer before presenting.");
        }

        VulkanTexture texture = _textures[_imageIndex];
        if (texture.State != ResourceState.Present)
        {
            throw new InvalidOperationException($"The back buffer must be moved to the Present state before presenting; it is in {texture.State}.");
        }

        Silk.NET.Vulkan.Semaphore ready = _presentSemaphores[_imageIndex];
        _device.SignalOnGpu(ready);
        SwapchainKHR handle = _handle;
        uint index = _imageIndex;
        PresentInfoKHR info = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &ready,
            SwapchainCount = 1,
            PSwapchains = &handle,
            PImageIndices = &index,
        };
        Result result = _swapchainApi.QueuePresent(_device.Queue, &info);
        _acquired = false;
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            _outOfDate = true;
            return;
        }

        VulkanResult.Check(result);
    }

    public void Resize(int width, int height)
    {
        Recreate(width, height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Destroy();
        _swapchainApi.DestroySwapchain(_device.Handle, _handle, null);
        _surfaceApi.DestroySurface(_device.Instance, _surface, null);
        _swapchainApi.Dispose();
        _surfaceApi.Dispose();
    }

    private SurfaceKHR CreateSurface(INativeWindow window)
    {
        Vk vk = _device.Vk;
        SurfaceKHR surface;
        if (window.Win32 is (nint hwnd, _, nint hinstance) && vk.TryGetInstanceExtension(_device.Instance, out KhrWin32Surface win32))
        {
            Win32SurfaceCreateInfoKHR info = new() { SType = StructureType.Win32SurfaceCreateInfoKhr, Hinstance = hinstance, Hwnd = hwnd };
            VulkanResult.Check(win32.CreateWin32Surface(_device.Instance, &info, null, &surface));
            win32.Dispose();
            return surface;
        }

        if (window.X11 is (nint display, nuint xWindow) && vk.TryGetInstanceExtension(_device.Instance, out KhrXlibSurface xlib))
        {
            XlibSurfaceCreateInfoKHR info = new() { SType = StructureType.XlibSurfaceCreateInfoKhr, Dpy = (nint*)display, Window = (nint)xWindow };
            VulkanResult.Check(xlib.CreateXlibSurface(_device.Instance, &info, null, &surface));
            xlib.Dispose();
            return surface;
        }

        // TODO(Linux): Wayland windows.
        throw new GraphicsUnavailableException($"Vulkan can't present to {window.Kind} windows here.");
    }

    /// <summary>Creates the swapchain, its back buffers and semaphores; the size follows the window when the surface fixes it.</summary>
    private void Create(int width, int height)
    {
        SurfaceCapabilitiesKHR capabilities;
        VulkanResult.Check(_surfaceApi.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _surface, &capabilities));
        CheckFormat();

        Extent2D extent = capabilities.CurrentExtent;
        if (extent.Width == uint.MaxValue)
        {
            uint clampedWidth = Math.Clamp((uint)width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            uint clampedHeight = Math.Clamp((uint)height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);
            extent = new Extent2D(clampedWidth, clampedHeight);
        }

        uint imageCount = Math.Max(PreferredImageCount, capabilities.MinImageCount);
        if (capabilities.MaxImageCount > 0)
        {
            imageCount = Math.Min(imageCount, capabilities.MaxImageCount);
        }

        ImageUsageFlags usage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit;
        SwapchainCreateInfoKHR info = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = SurfaceFormat,
            ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = usage,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = ChooseCompositeAlpha(capabilities.SupportedCompositeAlpha),
            PresentMode = PresentModeKHR.FifoKhr,
            Clipped = true,
            OldSwapchain = _handle,
        };
        SwapchainKHR old = _handle;
        VulkanResult.Check(_swapchainApi.CreateSwapchain(_device.Handle, &info, null, out _handle));
        if (old.Handle != 0)
        {
            _swapchainApi.DestroySwapchain(_device.Handle, old, null);
        }

        Width = (int)extent.Width;
        Height = (int)extent.Height;
        _device.SetName(ObjectType.SwapchainKhr, _handle.Handle, _name);
        CreateImages();
        _outOfDate = false;
    }

    private void CheckFormat()
    {
        uint count = 0;
        VulkanResult.Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _surface, &count, null));
        SurfaceFormatKHR[] formats = new SurfaceFormatKHR[count];
        fixed (SurfaceFormatKHR* pointer = formats)
        {
            VulkanResult.Check(_surfaceApi.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _surface, &count, pointer));
        }

        foreach (SurfaceFormatKHR format in formats)
        {
            if (format.Format == SurfaceFormat && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return;
            }
        }

        throw new GraphicsUnavailableException($"The window surface doesn't support {SurfaceFormat}.");
    }

    private static CompositeAlphaFlagsKHR ChooseCompositeAlpha(CompositeAlphaFlagsKHR supported)
    {
        foreach (CompositeAlphaFlagsKHR mode in CompositeAlphaPreference)
        {
            if (supported.HasFlag(mode))
            {
                return mode;
            }
        }

        return CompositeAlphaFlagsKHR.OpaqueBitKhr;
    }

    private void CreateImages()
    {
        uint count = 0;
        VulkanResult.Check(_swapchainApi.GetSwapchainImages(_device.Handle, _handle, &count, null));
        Image[] images = new Image[count];
        fixed (Image* pointer = images)
        {
            VulkanResult.Check(_swapchainApi.GetSwapchainImages(_device.Handle, _handle, &count, pointer));
        }

        TextureDescription description = new(Width, Height, Format, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = _name };
        _textures = new VulkanTexture[count];
        _acquireSemaphores = new Silk.NET.Vulkan.Semaphore[count];
        _acquireReleaseValues = new ulong[count];
        _presentSemaphores = new Silk.NET.Vulkan.Semaphore[count];
        for (int index = 0; index < count; index++)
        {
            _textures[index] = new VulkanTexture(_device, description, images[index]);
            _acquireSemaphores[index] = CreateSemaphore();
            _presentSemaphores[index] = CreateSemaphore();
        }

        _frame = 0;
    }

    private Silk.NET.Vulkan.Semaphore CreateSemaphore()
    {
        SemaphoreCreateInfo info = new() { SType = StructureType.SemaphoreCreateInfo };
        VulkanResult.Check(_device.Vk.CreateSemaphore(_device.Handle, &info, null, out Silk.NET.Vulkan.Semaphore semaphore));
        return semaphore;
    }

    private void Recreate(int width, int height)
    {
        Destroy();
        Create(width, height);
    }

    /// <summary>Waits for the queue, presentation included, then destroys the back buffer views and semaphores.</summary>
    private void Destroy()
    {
        _device.WaitQueueIdle();
        foreach (VulkanTexture texture in _textures)
        {
            texture.Dispose();
        }

        _device.WaitQueueIdle();
        foreach (Silk.NET.Vulkan.Semaphore semaphore in _acquireSemaphores)
        {
            _device.Vk.DestroySemaphore(_device.Handle, semaphore, null);
        }

        foreach (Silk.NET.Vulkan.Semaphore semaphore in _presentSemaphores)
        {
            _device.Vk.DestroySemaphore(_device.Handle, semaphore, null);
        }

        _acquired = false;
    }
}
