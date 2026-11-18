using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// A texture another graphics API shows, such as the editor's UI compositor, with two binary semaphores: Ready, signaled when a frame
/// is done, and Released, signaled by the importer when it has finished reading. Each frame: <see cref="BeginFrame"/>, render to
/// <see cref="Texture"/>, move it to the CopySource state, submit, <see cref="EndFrame"/>; then the importer waits on Ready, reads
/// the texture in the CopySource layout and signals Released before the next <see cref="BeginFrame"/>.
/// </summary>
public sealed unsafe class VulkanSharedTexture : IDisposable
{
    private readonly VulkanGraphicsDevice _device;
    private readonly VulkanTexture _texture;
    private readonly Silk.NET.Vulkan.Semaphore _ready;
    private readonly Silk.NET.Vulkan.Semaphore _released;
    private readonly ExternalMemoryHandleTypeFlags _memoryType;
    private readonly ExternalSemaphoreHandleTypeFlags _semaphoreType;
    private bool _handedOver;
    private bool _begun;
    private bool _disposed;

    internal VulkanSharedTexture(VulkanGraphicsDevice device, in TextureDescription description)
    {
        if (!device.CanShare)
        {
            throw new GraphicsUnavailableException("The Vulkan driver can't export memory and semaphores.");
        }

        _device = device;
        _memoryType = ExternalMemoryHandleTypeFlags.OpaqueFDBit;
        _semaphoreType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit;
        if (OperatingSystem.IsWindows())
        {
            _memoryType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit;
            _semaphoreType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit;
        }

        _texture = new VulkanTexture(device, description, 0, _memoryType);
        _ready = CreateSemaphore();
        _released = CreateSemaphore();
    }

    public ITexture Texture => _texture;

    /// <summary>Exports new OS handles for the memory and both semaphores; whoever imports them owns them.</summary>
    public VulkanSharedHandles Export()
    {
        nint memory = ExportMemory();
        nint ready = ExportSemaphore(_ready);
        nint released = ExportSemaphore(_released);
        return new VulkanSharedHandles(memory, _texture.MemorySize, ready, released);
    }

    /// <summary>Makes later submissions wait until the importer has released the frame it was handed last.</summary>
    public void BeginFrame()
    {
        if (_begun)
        {
            throw new InvalidOperationException("End the frame before beginning another.");
        }

        if (_handedOver)
        {
            _device.WaitOnGpu(_released);
        }

        _begun = true;
    }

    /// <summary>Signals Ready once the GPU has finished every command list submitted so far; the texture must be in the CopySource state.</summary>
    public void EndFrame()
    {
        if (!_begun)
        {
            throw new InvalidOperationException("Begin a frame before ending it.");
        }

        if (_texture.State != ResourceState.CopySource)
        {
            throw new InvalidOperationException($"The shared texture must be moved to the CopySource state before the frame ends; it is in {_texture.State}.");
        }

        _device.SignalOnGpu(_ready);
        _begun = false;
        _handedOver = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _texture.Dispose();
        Silk.NET.Vulkan.Semaphore ready = _ready;
        Silk.NET.Vulkan.Semaphore released = _released;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroySemaphore(device.Handle, ready, null);
            device.Vk.DestroySemaphore(device.Handle, released, null);
        });
    }

    private Silk.NET.Vulkan.Semaphore CreateSemaphore()
    {
        ExportSemaphoreCreateInfo export = new()
        {
            SType = StructureType.ExportSemaphoreCreateInfo,
            HandleTypes = _semaphoreType,
        };
        SemaphoreCreateInfo info = new() { SType = StructureType.SemaphoreCreateInfo, PNext = &export };
        VulkanResult.Check(_device.Vk.CreateSemaphore(_device.Handle, &info, null, out Silk.NET.Vulkan.Semaphore semaphore));
        return semaphore;
    }

    private nint ExportMemory()
    {
        Vk vk = _device.Vk;
        if (OperatingSystem.IsWindows())
        {
            vk.TryGetDeviceExtension(_device.Instance, _device.Handle, out KhrExternalMemoryWin32 win32);
            MemoryGetWin32HandleInfoKHR win32Info = new()
            {
                SType = StructureType.MemoryGetWin32HandleInfoKhr,
                Memory = _texture.Memory,
                HandleType = _memoryType,
            };
            nint handle = 0;
            VulkanResult.Check(win32.GetMemoryWin32Handle(_device.Handle, &win32Info, &handle));
            return handle;
        }

        vk.TryGetDeviceExtension(_device.Instance, _device.Handle, out KhrExternalMemoryFd fd);
        MemoryGetFdInfoKHR fdInfo = new()
        {
            SType = StructureType.MemoryGetFDInfoKhr,
            Memory = _texture.Memory,
            HandleType = _memoryType,
        };
        int descriptor = 0;
        VulkanResult.Check(fd.GetMemoryF(_device.Handle, &fdInfo, &descriptor));
        return descriptor;
    }

    private nint ExportSemaphore(Silk.NET.Vulkan.Semaphore semaphore)
    {
        Vk vk = _device.Vk;
        if (OperatingSystem.IsWindows())
        {
            vk.TryGetDeviceExtension(_device.Instance, _device.Handle, out KhrExternalSemaphoreWin32 win32);
            SemaphoreGetWin32HandleInfoKHR win32Info = new()
            {
                SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                Semaphore = semaphore,
                HandleType = _semaphoreType,
            };
            nint handle = 0;
            VulkanResult.Check(win32.GetSemaphoreWin32Handle(_device.Handle, &win32Info, &handle));
            return handle;
        }

        vk.TryGetDeviceExtension(_device.Instance, _device.Handle, out KhrExternalSemaphoreFd fd);
        SemaphoreGetFdInfoKHR fdInfo = new()
        {
            SType = StructureType.SemaphoreGetFDInfoKhr,
            Semaphore = semaphore,
            HandleType = _semaphoreType,
        };
        int descriptor = 0;
        VulkanResult.Check(fd.GetSemaphoreF(_device.Handle, &fdInfo, &descriptor));
        return descriptor;
    }
}
