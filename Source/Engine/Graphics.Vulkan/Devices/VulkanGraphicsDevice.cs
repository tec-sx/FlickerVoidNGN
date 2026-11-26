using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Presentation;
using FlickerVoid.Engine.Graphics.Resources;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Graphics.Vulkan.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Vulkan.Buffers;
using FlickerVoid.Engine.Graphics.Vulkan.Commands;
using FlickerVoid.Engine.Graphics.Vulkan.Descriptors;
using FlickerVoid.Engine.Graphics.Vulkan.Pipelines;
using FlickerVoid.Engine.Graphics.Vulkan.Presentation;
using FlickerVoid.Engine.Graphics.Vulkan.Samplers;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

using SamplerAddressMode = FlickerVoid.Engine.Graphics.Samplers.SamplerAddressMode;

namespace FlickerVoid.Engine.Graphics.Vulkan.Devices;

/// <summary>
/// A Vulkan 1.3 device: one graphics queue, a timeline semaphore counting submissions, bindless tables, and
/// resources released once the GPU has finished with them. Requires dynamic rendering, synchronization2 and descriptor indexing;
/// presents to windows when the driver has the surface and swapchain extensions, shares textures with other graphics APIs
/// when it can export memory and semaphores, traces rays when it has acceleration structures and ray queries, and upscales
/// with DLSS when asked to where NGX runs, with the instance and device extensions NGX names, denoising with DLSS Ray
/// Reconstruction too when asked to where NGX runs that.
/// </summary>
public sealed unsafe class VulkanGraphicsDevice : IGraphicsDevice
{
    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";
    private const string RayQueryExtension = "VK_KHR_ray_query";

    private readonly ILogger _logger;
    private readonly ExtDebugUtils? _debugUtils;
    private readonly DebugUtilsMessengerEXT _messenger;
    private readonly GCHandle _self;
    private readonly Silk.NET.Vulkan.Semaphore _timeline;
    private readonly PhysicalDeviceMemoryProperties _memoryProperties;
    private readonly Dictionary<ulong, ulong> _allocations = [];
    private readonly Queue<(ulong Value, Action Release)> _releases = new();
    private readonly VulkanCommandList _uploadCommands;
    private readonly List<IDisposable> _placeholders = [];
    private readonly KhrAccelerationStructure? _accelerationStructures;
    private readonly VulkanDlss? _dlss;
    private ulong _submitted;
    private ulong _acquireValue;
    private bool _disposed;

    public VulkanGraphicsDevice(GraphicsDeviceOptions options, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<VulkanGraphicsDevice>();
        _self = GCHandle.Alloc(this, GCHandleType.Weak);
        Vk = LoadVulkan();

        bool validation = options.Validation && HasLayer(ValidationLayer);
        if (options.Validation && !validation)
        {
            _logger.LogWarning("The Vulkan validation layer ({Layer}) is not installed; running without validation.", ValidationLayer);
        }

        VulkanDlss? dlss = null;
        if (options.Features.HasFlag(GraphicsFeatures.Dlss))
        {
            bool rayReconstruction = options.Features.HasFlag(GraphicsFeatures.RayReconstruction);
            dlss = VulkanDlss.TryLoad(Vk, _logger, rayReconstruction);
        }

        string[] dlssInstanceExtensions = dlss?.InstanceExtensions ?? [];
        Instance = CreateInstance(validation, dlssInstanceExtensions);
        if (validation && Vk.TryGetInstanceExtension(Instance, out ExtDebugUtils debugUtils))
        {
            _debugUtils = debugUtils;
            _messenger = CreateMessenger(debugUtils);
        }

        (PhysicalDevice, QueueFamily) = SelectPhysicalDevice();
        Vk.GetPhysicalDeviceProperties(PhysicalDevice, out PhysicalDeviceProperties properties);
        AdapterName = SilkMarshal.PtrToString((nint)properties.DeviceName) ?? "Vulkan device";
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out _memoryProperties);
        if (options.Features.HasFlag(GraphicsFeatures.RayQueries) && HasRayQueries(PhysicalDevice))
        {
            Features |= GraphicsFeatures.RayQueries;
        }

        string[]? dlssDeviceExtensions = dlss?.GetDeviceExtensions(Vk, Instance, PhysicalDevice);
        Handle = CreateDevice(dlssDeviceExtensions ?? []);
        if (dlssDeviceExtensions is not null && dlss!.Start(Vk, Instance, PhysicalDevice, Handle))
        {
            _dlss = dlss;
            Features |= GraphicsFeatures.Dlss;
            if (dlss.HasRayReconstruction)
            {
                Features |= GraphicsFeatures.RayReconstruction;
            }
        }
        else
        {
            dlss?.Dispose();
        }

        Vk.GetDeviceQueue(Handle, QueueFamily, 0, out Queue queue);
        Queue = queue;
        _timeline = CreateTimeline();

        PhysicalDeviceVulkan12Properties limits = GetVulkan12Properties(out PhysicalDeviceAccelerationStructurePropertiesKHR rayLimits);
        uint accelerationStructureSlots = 0;
        if (Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            Vk.TryGetDeviceExtension(Instance, Handle, out _accelerationStructures);
            ScratchAlignment = Math.Max(rayLimits.MinAccelerationStructureScratchOffsetAlignment, 1);
            accelerationStructureSlots = Math.Min(GraphicsLimits.BindlessAccelerationStructureCapacity, rayLimits.MaxDescriptorSetUpdateAfterBindAccelerationStructures);
        }

        Bindless = new VulkanBindlessTables(Vk, Handle, limits, accelerationStructureSlots);
        _uploadCommands = new VulkanCommandList(this);
        CreatePlaceholders();
        _logger.LogInformation("Vulkan device on {Adapter}.", AdapterName);
    }

    public string AdapterName { get; }

    public GraphicsFeatures Features { get; }

    internal Vk Vk { get; }

    internal Instance Instance { get; }

    internal PhysicalDevice PhysicalDevice { get; }

    internal Device Handle { get; }

    internal uint QueueFamily { get; }

    internal Queue Queue { get; }

    /// <summary>Whether the instance has the surface extensions and the device the swapchain extension.</summary>
    internal bool CanPresent { get; private set; }

    /// <summary>Whether the device can export memory and semaphores as the OS's handles, which <see cref="CreateSharedTexture"/> needs.</summary>
    internal bool CanShare { get; private set; }

    internal VulkanBindlessTables Bindless { get; }

    /// <summary>The alignment of the scratch memory acceleration structure builds take; 0 without ray queries.</summary>
    internal uint ScratchAlignment { get; }

    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default)
    {
        BufferUsageFlags extraUsage = 0;
        bool upload = !initialData.IsEmpty && description.Memory == MemoryLocation.Gpu;
        if (upload)
        {
            extraUsage = BufferUsageFlags.TransferDstBit;
        }

        VulkanBuffer buffer = new(this, description, extraUsage);
        if (initialData.IsEmpty)
        {
            return buffer;
        }

        if (description.Memory == MemoryLocation.Upload)
        {
            buffer.Write(initialData);
            return buffer;
        }

        if (description.Memory == MemoryLocation.Readback)
        {
            buffer.Dispose();
            throw new ArgumentException("Readback buffers can't take initial data.", nameof(initialData));
        }

        UploadBuffer(buffer, initialData);
        return buffer;
    }

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default)
    {
        bool fillsMipLevels = false;
        if (!initialData.IsEmpty)
        {
            fillsMipLevels = CheckInitialData(description, initialData.Length);
        }

        ImageUsageFlags extraUsage = 0;
        if (!initialData.IsEmpty)
        {
            extraUsage = ImageUsageFlags.TransferDstBit;
        }

        // Lower mip levels are blitted from the level above.
        if (fillsMipLevels)
        {
            extraUsage |= ImageUsageFlags.TransferSrcBit;
        }

        VulkanTexture texture = new(this, description, extraUsage);
        if (!initialData.IsEmpty)
        {
            UploadTexture(texture, initialData, fillsMipLevels);
        }

        return texture;
    }

    public ISampler CreateSampler(in SamplerDescription description) => new VulkanSampler(this, description);

    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description) => new VulkanPipeline(this, description);

    public IPipeline CreateComputePipeline(ComputePipelineDescription description) => new VulkanPipeline(this, description);

    public IAccelerationStructure CreateBottomLevel(in BottomLevelDescription description)
    {
        RequireAccelerationStructures();
        return VulkanAccelerationStructure.CreateBottomLevel(this, description);
    }

    public IAccelerationStructure CreateTopLevel(in TopLevelDescription description)
    {
        RequireAccelerationStructures();
        return VulkanAccelerationStructure.CreateTopLevel(this, description);
    }

    public IUpscaler CreateUpscaler(in UpscalerDescription description)
    {
        if (_dlss is null)
        {
            throw new NotSupportedException($"{AdapterName} has no DLSS; check {nameof(Features)} first.");
        }

        if (description.Mode == UpscaleMode.Off)
        {
            throw new NotSupportedException("Upscaling off needs no upscaler.");
        }

        if (description.Denoises && !Features.HasFlag(GraphicsFeatures.RayReconstruction))
        {
            throw new NotSupportedException($"{AdapterName} has no DLSS Ray Reconstruction; check {nameof(Features)} first.");
        }

        return new VulkanUpscaler(this, _dlss, description);
    }

    public ICommandList CreateCommandList() => new VulkanCommandList(this);

    /// <summary>
    /// Creates a texture another graphics API imports, such as the editor's UI compositor; throws
    /// <see cref="GraphicsUnavailableException"/> when the driver can't export memory and semaphores.
    /// </summary>
    public VulkanSharedTexture CreateSharedTexture(in TextureDescription description) => new(this, description);

    public ISwapchain CreateSwapchain(in SwapchainDescription description) => new VulkanSwapchain(this, description);

    /// <summary>Submits the command list after the last acquired swapchain image is free, so it may render to it.</summary>
    public void Submit(ICommandList commandList)
    {
        VulkanCommandList vulkanCommands = (VulkanCommandList)commandList;
        _submitted++;
        CommandBufferSubmitInfo commandInfo = new()
        {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = vulkanCommands.Handle,
        };
        SemaphoreSubmitInfo wait = TimelineInfo(_acquireValue);
        SemaphoreSubmitInfo signal = TimelineInfo(_submitted);
        SubmitInfo2 submit = new()
        {
            SType = StructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = 1,
            PWaitSemaphoreInfos = &wait,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandInfo,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signal,
        };
        VulkanResult.Check(Vk.QueueSubmit2(Queue, 1, &submit, default));
        vulkanCommands.SubmissionValue = _submitted;
        RunCompletedReleases();
    }

    public void WaitIdle()
    {
        WaitFor(_submitted);
        RunCompletedReleases();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (IDisposable placeholder in _placeholders)
        {
            placeholder.Dispose();
        }

        _uploadCommands.Dispose();
        VulkanResult.Check(Vk.DeviceWaitIdle(Handle));
        while (_releases.TryDequeue(out (ulong Value, Action Release) release))
        {
            release.Release();
        }

        _dlss?.Dispose();
        Bindless.Dispose();
        _accelerationStructures?.Dispose();
        Vk.DestroySemaphore(Handle, _timeline, null);
        Vk.DestroyDevice(Handle, null);
        if (_debugUtils is not null)
        {
            _debugUtils.DestroyDebugUtilsMessenger(Instance, _messenger, null);
            _debugUtils.Dispose();
        }

        Vk.DestroyInstance(Instance, null);
        Vk.Dispose();
        _self.Free();
    }

    /// <summary>The acceleration structure commands; throws <see cref="NotSupportedException"/> on a device without ray queries.</summary>
    internal KhrAccelerationStructure RequireAccelerationStructures()
    {
        if (_accelerationStructures is null)
        {
            throw new NotSupportedException($"{AdapterName} was created without ray queries; check {nameof(Features)} first.");
        }

        return _accelerationStructures;
    }

    /// <summary>Runs the release once the GPU has finished everything submitted so far.</summary>
    internal void ReleaseLater(Action release)
    {
        _releases.Enqueue((_submitted, release));
    }

    /// <summary>
    /// Makes later submissions wait for the binary semaphore, such as a swapchain's image-acquired semaphore, and returns the
    /// timeline value after which the semaphore can be reused.
    /// </summary>
    internal ulong WaitOnGpu(Silk.NET.Vulkan.Semaphore semaphore)
    {
        _submitted++;
        SemaphoreSubmitInfo wait = new()
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = semaphore,
            StageMask = PipelineStageFlags2.AllCommandsBit,
        };
        SemaphoreSubmitInfo signal = TimelineInfo(_submitted);
        SubmitInfo2 submit = new()
        {
            SType = StructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = 1,
            PWaitSemaphoreInfos = &wait,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signal,
        };
        VulkanResult.Check(Vk.QueueSubmit2(Queue, 1, &submit, default));
        _acquireValue = _submitted;
        return _submitted;
    }

    /// <summary>Signals the binary semaphore, such as a swapchain's ready-to-present semaphore, once the GPU has finished everything submitted so far.</summary>
    internal void SignalOnGpu(Silk.NET.Vulkan.Semaphore semaphore)
    {
        SemaphoreSubmitInfo wait = TimelineInfo(_submitted);
        SemaphoreSubmitInfo signal = new()
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = semaphore,
            StageMask = PipelineStageFlags2.AllCommandsBit,
        };
        SubmitInfo2 submit = new()
        {
            SType = StructureType.SubmitInfo2,
            WaitSemaphoreInfoCount = 1,
            PWaitSemaphoreInfos = &wait,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signal,
        };
        VulkanResult.Check(Vk.QueueSubmit2(Queue, 1, &submit, default));
    }

    /// <summary>Blocks until the queue has finished everything, presentation included, then releases what was waiting for it.</summary>
    internal void WaitQueueIdle()
    {
        VulkanResult.Check(Vk.QueueWaitIdle(Queue));
        RunCompletedReleases();
    }

    internal void WaitFor(ulong value)
    {
        if (value == 0)
        {
            return;
        }

        Silk.NET.Vulkan.Semaphore timeline = _timeline;
        SemaphoreWaitInfo info = new()
        {
            SType = StructureType.SemaphoreWaitInfo,
            SemaphoreCount = 1,
            PSemaphores = &timeline,
            PValues = &value,
        };
        VulkanResult.Check(Vk.WaitSemaphores(Handle, &info, ulong.MaxValue));
    }

    /// <summary>Allocates memory with the properties; <paramref name="next"/> chains allocation structures such as export info.</summary>
    internal DeviceMemory AllocateMemory(in MemoryRequirements requirements, MemoryPropertyFlags properties, void* next = null)
    {
        uint typeIndex = FindMemoryType(requirements.MemoryTypeBits, properties);
        MemoryAllocateInfo info = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            PNext = next,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = typeIndex,
        };
        VulkanResult.Check(Vk.AllocateMemory(Handle, &info, null, out DeviceMemory memory));
        lock (_allocations)
        {
            _allocations[memory.Handle] = requirements.Size;
        }

        return memory;
    }

    /// <summary>Frees memory from <see cref="AllocateMemory"/>.</summary>
    internal void FreeMemory(DeviceMemory memory)
    {
        lock (_allocations)
        {
            _allocations.Remove(memory.Handle);
        }

        Vk.FreeMemory(Handle, memory, null);
    }

    public GraphicsInfo GetInfo()
    {
        long deviceMemory = 0;
        for (int index = 0; index < _memoryProperties.MemoryHeapCount; index++)
        {
            MemoryHeap heap = _memoryProperties.MemoryHeaps[index];
            if (heap.Flags.HasFlag(MemoryHeapFlags.DeviceLocalBit))
            {
                deviceMemory += (long)heap.Size;
            }
        }

        long allocated;
        int count;
        lock (_allocations)
        {
            allocated = (long)_allocations.Values.Sum(static size => (decimal)size);
            count = _allocations.Count;
        }

        Vk.GetPhysicalDeviceProperties(PhysicalDevice, out PhysicalDeviceProperties properties);
        uint packed = properties.ApiVersion;
        string version = $"{(packed >> 22) & 0x7F}.{(packed >> 12) & 0x3FF}.{packed & 0xFFF}";
        return new GraphicsInfo(AdapterName, version, deviceMemory, allocated, count);
    }

    internal void SetName(ObjectType type, ulong handle, string? name)
    {
        if (_debugUtils is null || name is null)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(name + '\0');
        fixed (byte* pointer = bytes)
        {
            DebugUtilsObjectNameInfoEXT info = new()
            {
                SType = StructureType.DebugUtilsObjectNameInfoExt,
                ObjectType = type,
                ObjectHandle = handle,
                PObjectName = pointer,
            };
            _debugUtils.SetDebugUtilsObjectName(Handle, &info);
        }
    }

    private static Vk LoadVulkan()
    {
        try
        {
            return Vk.GetApi();
        }
        catch (Exception exception) when (exception is DllNotFoundException or FileNotFoundException or EntryPointNotFoundException)
        {
            throw new GraphicsUnavailableException($"The Vulkan loader is not installed: {exception.Message}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static Bool32 OnDebugMessage(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT types, DebugUtilsMessengerCallbackDataEXT* data, void* userData)
    {
        GCHandle handle = GCHandle.FromIntPtr((nint)userData);
        if (handle.Target is VulkanGraphicsDevice device)
        {
            string message = SilkMarshal.PtrToString((nint)data->PMessage) ?? string.Empty;
            device.LogDebugMessage(severity, message);
        }

        return Vk.False;
    }

    private void LogDebugMessage(DebugUtilsMessageSeverityFlagsEXT severity, string message)
    {
        if (severity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt))
        {
            _logger.LogError("Vulkan: {Message}", message);
            return;
        }

        if (severity.HasFlag(DebugUtilsMessageSeverityFlagsEXT.WarningBitExt))
        {
            _logger.LogWarning("Vulkan: {Message}", message);
        }
    }

    private bool HasLayer(string name)
    {
        uint count = 0;
        VulkanResult.Check(Vk.EnumerateInstanceLayerProperties(&count, null));
        LayerProperties[] layers = new LayerProperties[count];
        fixed (LayerProperties* pointer = layers)
        {
            VulkanResult.Check(Vk.EnumerateInstanceLayerProperties(&count, pointer));
        }

        foreach (LayerProperties layer in layers)
        {
            string? layerName = SilkMarshal.PtrToString((nint)layer.LayerName);
            if (layerName == name)
            {
                return true;
            }
        }

        return false;
    }

    private Instance CreateInstance(bool validation, string[] extraExtensions)
    {
        byte[] engineName = "FlickerVoid"u8.ToArray();
        List<string> layers = [];
        List<string> extensions = [];
        if (validation)
        {
            layers.Add(ValidationLayer);
            extensions.Add(ExtDebugUtils.ExtensionName);
        }

        string windowSurface = GetWindowSurfaceExtension();
        bool surfaces = Vk.IsInstanceExtensionPresent(KhrSurface.ExtensionName) && Vk.IsInstanceExtensionPresent(windowSurface);
        if (surfaces)
        {
            extensions.Add(KhrSurface.ExtensionName);
            extensions.Add(windowSurface);
        }

        CanPresent = surfaces;
        AddMissing(extensions, extraExtensions);

        nint layerNames = SilkMarshal.StringArrayToPtr(layers);
        nint extensionNames = SilkMarshal.StringArrayToPtr(extensions);
        try
        {
            fixed (byte* engine = engineName)
            {
                ApplicationInfo application = new()
                {
                    SType = StructureType.ApplicationInfo,
                    PEngineName = engine,
                    ApiVersion = Vk.Version13,
                };
                InstanceCreateInfo info = new()
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &application,
                    EnabledLayerCount = (uint)layers.Count,
                    PpEnabledLayerNames = (byte**)layerNames,
                    EnabledExtensionCount = (uint)extensions.Count,
                    PpEnabledExtensionNames = (byte**)extensionNames,
                };
                Result result = Vk.CreateInstance(&info, null, out Instance instance);
                if (result != Result.Success)
                {
                    throw new GraphicsUnavailableException($"Vulkan 1.3 instance creation failed: {result}.");
                }

                return instance;
            }
        }
        finally
        {
            SilkMarshal.Free(layerNames);
            SilkMarshal.Free(extensionNames);
        }
    }

    /// <summary>The extension that creates surfaces for the OS's windows.</summary>
    private static string GetWindowSurfaceExtension()
    {
        if (OperatingSystem.IsWindows())
        {
            return KhrWin32Surface.ExtensionName;
        }

        // TODO(Linux): Wayland surfaces; SDL opens X11 windows unless told otherwise.
        return KhrXlibSurface.ExtensionName;
    }

    /// <summary>The extensions that export memory and semaphores as the OS's handles: NT handles on Windows, file descriptors elsewhere.</summary>
    private static string[] GetSharingExtensions()
    {
        if (OperatingSystem.IsWindows())
        {
            return [KhrExternalMemoryWin32.ExtensionName, KhrExternalSemaphoreWin32.ExtensionName];
        }

        return [KhrExternalMemoryFd.ExtensionName, KhrExternalSemaphoreFd.ExtensionName];
    }

    private DebugUtilsMessengerEXT CreateMessenger(ExtDebugUtils debugUtils)
    {
        DebugUtilsMessengerCreateInfoEXT info = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(&OnDebugMessage),
            PUserData = (void*)GCHandle.ToIntPtr(_self),
        };
        VulkanResult.Check(debugUtils.CreateDebugUtilsMessenger(Instance, &info, null, out DebugUtilsMessengerEXT messenger));
        return messenger;
    }

    /// <summary>Picks a Vulkan 1.3 GPU with the features the engine needs and a graphics queue, preferring discrete, then integrated GPUs.</summary>
    private (PhysicalDevice Device, uint QueueFamily) SelectPhysicalDevice()
    {
        uint count = 0;
        VulkanResult.Check(Vk.EnumeratePhysicalDevices(Instance, &count, null));
        PhysicalDevice[] devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* pointer = devices)
        {
            VulkanResult.Check(Vk.EnumeratePhysicalDevices(Instance, &count, pointer));
        }

        PhysicalDevice best = default;
        uint bestFamily = 0;
        int bestScore = -1;
        foreach (PhysicalDevice device in devices)
        {
            if (!IsSuitable(device, out uint family))
            {
                continue;
            }

            Vk.GetPhysicalDeviceProperties(device, out PhysicalDeviceProperties properties);
            int score = GetTypeScore(properties.DeviceType);
            if (score > bestScore)
            {
                best = device;
                bestFamily = family;
                bestScore = score;
            }
        }

        if (bestScore < 0)
        {
            throw new GraphicsUnavailableException("No GPU supports Vulkan 1.3 with dynamic rendering, synchronization2, timeline semaphores and bindless descriptor indexing.");
        }

        return (best, bestFamily);
    }

    private static int GetTypeScore(PhysicalDeviceType type)
    {
        switch (type)
        {
            case PhysicalDeviceType.DiscreteGpu:
                return 3;
            case PhysicalDeviceType.IntegratedGpu:
                return 2;
            case PhysicalDeviceType.VirtualGpu:
                return 1;
            default:
                return 0;
        }
    }

    private bool IsSuitable(PhysicalDevice device, out uint queueFamily)
    {
        queueFamily = 0;
        Vk.GetPhysicalDeviceProperties(device, out PhysicalDeviceProperties properties);
        if (properties.ApiVersion < Vk.Version13)
        {
            return false;
        }

        PhysicalDeviceVulkan13Features features13 = new() { SType = StructureType.PhysicalDeviceVulkan13Features };
        PhysicalDeviceVulkan12Features features12 = new() { SType = StructureType.PhysicalDeviceVulkan12Features, PNext = &features13 };
        PhysicalDeviceFeatures2 features = new() { SType = StructureType.PhysicalDeviceFeatures2, PNext = &features12 };
        Vk.GetPhysicalDeviceFeatures2(device, &features);
        bool supported = features13.DynamicRendering && features13.Synchronization2
            && features12.TimelineSemaphore && features12.DescriptorIndexing && features12.RuntimeDescriptorArray
            && features12.DescriptorBindingPartiallyBound && features12.DescriptorBindingUpdateUnusedWhilePending
            && features12.DescriptorBindingSampledImageUpdateAfterBind && features12.DescriptorBindingStorageBufferUpdateAfterBind
            && features12.DescriptorBindingStorageImageUpdateAfterBind
            && features12.ShaderSampledImageArrayNonUniformIndexing && features12.ShaderStorageBufferArrayNonUniformIndexing
            && features12.ShaderStorageImageArrayNonUniformIndexing && features.Features.ShaderStorageImageWriteWithoutFormat
            && features.Features.TextureCompressionBC;
        if (!supported)
        {
            return false;
        }

        return TryFindGraphicsQueue(device, out queueFamily);
    }

    private bool TryFindGraphicsQueue(PhysicalDevice device, out uint queueFamily)
    {
        uint count = 0;
        Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        QueueFamilyProperties[] families = new QueueFamilyProperties[count];
        fixed (QueueFamilyProperties* pointer = families)
        {
            Vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, pointer);
        }

        for (uint index = 0; index < count; index++)
        {
            if (families[index].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamily = index;
                return true;
            }
        }

        queueFamily = 0;
        return false;
    }

    /// <summary>Whether the GPU builds acceleration structures and its shaders trace rays against them, from a bindless table.</summary>
    private bool HasRayQueries(PhysicalDevice device)
    {
        string[] extensions = [KhrAccelerationStructure.ExtensionName, RayQueryExtension, KhrDeferredHostOperations.ExtensionName];
        if (!Array.TrueForAll(extensions, extension => Vk.IsDeviceExtensionPresent(device, extension)))
        {
            return false;
        }

        PhysicalDeviceRayQueryFeaturesKHR rayQuery = new() { SType = StructureType.PhysicalDeviceRayQueryFeaturesKhr };
        PhysicalDeviceAccelerationStructureFeaturesKHR structures = new() { SType = StructureType.PhysicalDeviceAccelerationStructureFeaturesKhr, PNext = &rayQuery };
        PhysicalDeviceVulkan12Features features12 = new() { SType = StructureType.PhysicalDeviceVulkan12Features, PNext = &structures };
        PhysicalDeviceFeatures2 features = new() { SType = StructureType.PhysicalDeviceFeatures2, PNext = &features12 };
        Vk.GetPhysicalDeviceFeatures2(device, &features);
        return features12.BufferDeviceAddress && structures.AccelerationStructure && structures.DescriptorBindingAccelerationStructureUpdateAfterBind && rayQuery.RayQuery;
    }

    /// <summary>Creates the device with its features and extensions, and the extra extensions DLSS needs, which turn on buffer device addresses too.</summary>
    private Device CreateDevice(string[] extraExtensions)
    {
        float priority = 1;
        DeviceQueueCreateInfo queue = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = QueueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };
        PhysicalDeviceVulkan13Features features13 = new()
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,

            // HLSL's discard and clip() compile to demoting the pixel to a helper; Vulkan 1.3 devices all support it.
            ShaderDemoteToHelperInvocation = true,
        };
        PhysicalDeviceVulkan12Features features12 = new()
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            PNext = &features13,
            TimelineSemaphore = true,
            DescriptorIndexing = true,
            RuntimeDescriptorArray = true,
            DescriptorBindingPartiallyBound = true,
            DescriptorBindingUpdateUnusedWhilePending = true,
            DescriptorBindingSampledImageUpdateAfterBind = true,
            DescriptorBindingStorageBufferUpdateAfterBind = true,
            DescriptorBindingStorageImageUpdateAfterBind = true,
            ShaderSampledImageArrayNonUniformIndexing = true,
            ShaderStorageBufferArrayNonUniformIndexing = true,
            ShaderStorageImageArrayNonUniformIndexing = true,
        };
        PhysicalDeviceFeatures2 features = new()
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &features12,
            Features = new PhysicalDeviceFeatures { ShaderStorageImageWriteWithoutFormat = true, TextureCompressionBC = true },
        };
        List<string> extensions = [];
        PhysicalDeviceRayQueryFeaturesKHR rayQuery = new() { SType = StructureType.PhysicalDeviceRayQueryFeaturesKhr, RayQuery = true };
        PhysicalDeviceAccelerationStructureFeaturesKHR structures = new()
        {
            SType = StructureType.PhysicalDeviceAccelerationStructureFeaturesKhr,
            PNext = &rayQuery,
            AccelerationStructure = true,
            DescriptorBindingAccelerationStructureUpdateAfterBind = true,
        };
        if (Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            features13.PNext = &structures;
            features12.BufferDeviceAddress = true;
            extensions.Add(KhrAccelerationStructure.ExtensionName);
            extensions.Add(RayQueryExtension);
            extensions.Add(KhrDeferredHostOperations.ExtensionName);
        }

        CanPresent = CanPresent && Vk.IsDeviceExtensionPresent(PhysicalDevice, KhrSwapchain.ExtensionName);
        if (CanPresent)
        {
            extensions.Add(KhrSwapchain.ExtensionName);
        }

        string[] sharing = GetSharingExtensions();
        CanShare = Array.TrueForAll(sharing, extension => Vk.IsDeviceExtensionPresent(PhysicalDevice, extension));
        if (CanShare)
        {
            extensions.AddRange(sharing);
        }

        if (extraExtensions.Length > 0)
        {
            features12.BufferDeviceAddress = true;
            AddMissing(extensions, extraExtensions);
        }

        nint extensionNames = SilkMarshal.StringArrayToPtr(extensions);
        try
        {
            DeviceCreateInfo info = new()
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &features,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queue,
                EnabledExtensionCount = (uint)extensions.Count,
                PpEnabledExtensionNames = (byte**)extensionNames,
            };
            VulkanResult.Check(Vk.CreateDevice(PhysicalDevice, &info, null, out Device device));
            return device;
        }
        finally
        {
            SilkMarshal.Free(extensionNames);
        }
    }

    private static void AddMissing(List<string> extensions, string[] extraExtensions)
    {
        foreach (string extension in extraExtensions)
        {
            if (!extensions.Contains(extension))
            {
                extensions.Add(extension);
            }
        }
    }

    private Silk.NET.Vulkan.Semaphore CreateTimeline()
    {
        SemaphoreTypeCreateInfo type = new()
        {
            SType = StructureType.SemaphoreTypeCreateInfo,
            SemaphoreType = SemaphoreType.Timeline,
            InitialValue = 0,
        };
        SemaphoreCreateInfo info = new() { SType = StructureType.SemaphoreCreateInfo, PNext = &type };
        VulkanResult.Check(Vk.CreateSemaphore(Handle, &info, null, out Silk.NET.Vulkan.Semaphore semaphore));
        return semaphore;
    }

    /// <summary>The descriptor limits, and with ray queries the acceleration structure limits.</summary>
    private PhysicalDeviceVulkan12Properties GetVulkan12Properties(out PhysicalDeviceAccelerationStructurePropertiesKHR rayLimits)
    {
        PhysicalDeviceAccelerationStructurePropertiesKHR structures = new() { SType = StructureType.PhysicalDeviceAccelerationStructurePropertiesKhr };
        PhysicalDeviceVulkan12Properties properties12 = new() { SType = StructureType.PhysicalDeviceVulkan12Properties };
        if (Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            properties12.PNext = &structures;
        }

        PhysicalDeviceProperties2 properties = new() { SType = StructureType.PhysicalDeviceProperties2, PNext = &properties12 };
        Vk.GetPhysicalDeviceProperties2(PhysicalDevice, &properties);
        rayLimits = structures;
        return properties12;
    }

    private uint FindMemoryType(uint typeBits, MemoryPropertyFlags properties)
    {
        for (uint index = 0; index < _memoryProperties.MemoryTypeCount; index++)
        {
            bool allowed = (typeBits & (1u << (int)index)) != 0;
            MemoryPropertyFlags flags = _memoryProperties.MemoryTypes[(int)index].PropertyFlags;
            if (allowed && (flags & properties) == properties)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"No Vulkan memory type has {properties}.");
    }

    private SemaphoreSubmitInfo TimelineInfo(ulong value) => new()
    {
        SType = StructureType.SemaphoreSubmitInfo,
        Semaphore = _timeline,
        Value = value,
        StageMask = PipelineStageFlags2.AllCommandsBit,
    };

    private void RunCompletedReleases()
    {
        VulkanResult.Check(Vk.GetSemaphoreCounterValue(Handle, _timeline, out ulong completed));
        while (_releases.TryPeek(out (ulong Value, Action Release) release) && release.Value <= completed)
        {
            _releases.Dequeue();
            release.Release();
        }
    }

    /// <summary>Copies the data through a staging buffer and waits, leaving the buffer in the state its usage reads it in.</summary>
    private void UploadBuffer(VulkanBuffer buffer, ReadOnlySpan<byte> data)
    {
        BufferDescription stagingDescription = new(data.Length, BufferUsage.CopySource, MemoryLocation.Upload);
        using VulkanBuffer staging = new(this, stagingDescription, 0);
        staging.Write(data);

        _uploadCommands.Begin();
        _uploadCommands.Barrier(buffer, ResourceState.CopyDestination);
        _uploadCommands.CopyBuffer(staging, 0, buffer, 0, data.Length);
        ResourceState final = GetReadState(buffer.Description.Usage);
        if (final != ResourceState.CopyDestination)
        {
            _uploadCommands.Barrier(buffer, final);
        }

        _uploadCommands.End();
        Submit(_uploadCommands);
        WaitIdle();
    }

    /// <summary>
    /// Initial data holds every mip level's tightly packed rows, or only the top level's, from which the device filters the
    /// others; block-compressed textures must give every level. Returns whether the device fills the lower levels.
    /// </summary>
    private static bool CheckInitialData(in TextureDescription description, int length)
    {
        int allLevels = description.GetDataSize();
        if (length == allLevels)
        {
            return false;
        }

        int topLevel = description.GetLevelSize(0);
        if (length == topLevel && !description.Format.IsBlockCompressed())
        {
            return true;
        }

        throw new ArgumentException($"Expected {allLevels} bytes of tightly packed rows for every level, or {topLevel} for the top level of an uncompressed texture, got {length}.", "initialData");
    }

    /// <summary>
    /// Copies each given level's tightly packed rows through a staging buffer with padded rows, filters the other levels
    /// from the top one when only it was given, and waits, leaving shader-readable textures in ShaderRead.
    /// </summary>
    private void UploadTexture(VulkanTexture texture, ReadOnlySpan<byte> data, bool fillsMipLevels)
    {
        TextureDescription description = texture.Description;
        int levels = description.MipLevels;
        if (fillsMipLevels)
        {
            levels = 1;
        }

        long stagingSize = 0;
        for (int level = 0; level < levels; level++)
        {
            TextureDescription mip = description.GetMipDescription(level);
            stagingSize += (long)description.Format.GetCopyRowPitch(mip.Width) * description.Format.GetRowCount(mip.Height);
        }

        BufferDescription stagingDescription = new(stagingSize, BufferUsage.CopySource, MemoryLocation.Upload);
        using VulkanBuffer staging = new(this, stagingDescription, 0);
        _uploadCommands.Begin();
        _uploadCommands.Barrier(texture, ResourceState.CopyDestination);
        int dataOffset = 0;
        long stagingOffset = 0;
        for (int level = 0; level < levels; level++)
        {
            TextureDescription mip = description.GetMipDescription(level);
            int rowSize = description.Format.GetRowSize(mip.Width);
            int rowCount = description.Format.GetRowCount(mip.Height);
            int pitch = description.Format.GetCopyRowPitch(mip.Width);
            for (int row = 0; row < rowCount; row++)
            {
                ReadOnlySpan<byte> rowData = data.Slice(dataOffset + row * rowSize, rowSize);
                staging.Write(rowData, stagingOffset + (long)row * pitch);
            }

            _uploadCommands.CopyBufferToTexture(staging, stagingOffset, texture, level);
            dataOffset += rowSize * rowCount;
            stagingOffset += (long)pitch * rowCount;
        }

        if (fillsMipLevels)
        {
            _uploadCommands.FillMipLevels(texture);
        }

        if (description.Usage.HasFlag(TextureUsage.ShaderRead))
        {
            _uploadCommands.Barrier(texture, ResourceState.ShaderRead);
        }

        _uploadCommands.End();
        Submit(_uploadCommands);
        WaitIdle();
    }

    private static ResourceState GetReadState(BufferUsage usage)
    {
        if (usage.HasFlag(BufferUsage.ShaderRead))
        {
            return ResourceState.ShaderRead;
        }

        if (usage.HasFlag(BufferUsage.Index))
        {
            return ResourceState.IndexBuffer;
        }

        return ResourceState.CopyDestination;
    }

    /// <summary>Fills slot 0 of each bindless table, so shaders reading a missing resource get zeros.</summary>
    private void CreatePlaceholders()
    {
        TextureDescription textureDescription = new(1, 1, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "Bindless placeholder" };
        byte[] black = new byte[4];
        VulkanTexture texture = (VulkanTexture)CreateTexture(textureDescription, black);
        Bindless.WriteTexture(BindlessIndex.None, texture.View);

        BufferDescription bufferDescription = new(16, BufferUsage.ShaderRead) { Name = "Bindless placeholder" };
        byte[] zeros = new byte[16];
        VulkanBuffer buffer = (VulkanBuffer)CreateBuffer(bufferDescription, zeros);
        Bindless.WriteBuffer(BindlessIndex.None, buffer.Handle);

        SamplerDescription samplerDescription = new(SamplerFilter.Linear, SamplerAddressMode.Clamp);
        VulkanSampler sampler = (VulkanSampler)CreateSampler(samplerDescription);
        Bindless.WriteSampler(BindlessIndex.None, sampler.Handle);

        _placeholders.Add(texture);
        _placeholders.Add(buffer);
        _placeholders.Add(sampler);
        if (Features.HasFlag(GraphicsFeatures.RayQueries))
        {
            CreateEmptyScene();
        }
    }

    /// <summary>Fills slot 0 of the acceleration structure table with a top-level structure of no instances, which rays pass through.</summary>
    private void CreateEmptyScene()
    {
        BufferDescription instancesDescription = new(Marshal.SizeOf<AccelerationStructureInstance>(), BufferUsage.AccelerationStructureInput, MemoryLocation.Upload) { Name = "Bindless placeholder instances" };
        using IBuffer instances = CreateBuffer(instancesDescription);
        TopLevelDescription sceneDescription = new(1) { Name = "Bindless placeholder" };
        VulkanAccelerationStructure scene = VulkanAccelerationStructure.CreateTopLevel(this, sceneDescription);
        _uploadCommands.Begin();
        _uploadCommands.BuildTopLevel(scene, instances, 0);
        _uploadCommands.End();
        Submit(_uploadCommands);
        WaitIdle();
        Bindless.WriteAccelerationStructure(BindlessIndex.None, scene.Handle);
        _placeholders.Add(scene);
    }
}

