using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// A Vulkan 1.3 device: one graphics queue, a timeline semaphore counting submissions, bindless tables, and
/// resources released once the GPU has finished with them. Requires dynamic rendering, synchronization2 and descriptor indexing.
/// </summary>
public sealed unsafe class VulkanGraphicsDevice : IGraphicsDevice
{
    private const string ValidationLayer = "VK_LAYER_KHRONOS_validation";

    private readonly ILogger _logger;
    private readonly Instance _instance;
    private readonly PhysicalDevice _physicalDevice;
    private readonly ExtDebugUtils? _debugUtils;
    private readonly DebugUtilsMessengerEXT _messenger;
    private readonly GCHandle _self;
    private readonly Queue _queue;
    private readonly Silk.NET.Vulkan.Semaphore _timeline;
    private readonly PhysicalDeviceMemoryProperties _memoryProperties;
    private readonly Queue<(ulong Value, Action Release)> _releases = new();
    private readonly VulkanCommandList _uploadCommands;
    private readonly List<IDisposable> _placeholders = [];
    private ulong _submitted;
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

        _instance = CreateInstance(validation);
        if (validation && Vk.TryGetInstanceExtension(_instance, out ExtDebugUtils debugUtils))
        {
            _debugUtils = debugUtils;
            _messenger = CreateMessenger(debugUtils);
        }

        (_physicalDevice, QueueFamily) = SelectPhysicalDevice();
        Vk.GetPhysicalDeviceProperties(_physicalDevice, out PhysicalDeviceProperties properties);
        AdapterName = SilkMarshal.PtrToString((nint)properties.DeviceName) ?? "Vulkan device";
        Vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out _memoryProperties);

        Handle = CreateDevice();
        Vk.GetDeviceQueue(Handle, QueueFamily, 0, out _queue);
        _timeline = CreateTimeline();

        PhysicalDeviceVulkan12Properties limits = GetVulkan12Properties();
        Bindless = new VulkanBindlessTables(Vk, Handle, limits);
        _uploadCommands = new VulkanCommandList(this);
        CreatePlaceholders();
        _logger.LogInformation("Vulkan device on {Adapter}.", AdapterName);
    }

    public GraphicsApi Api => GraphicsApi.Vulkan;

    public string AdapterName { get; }

    internal Vk Vk { get; }

    internal Device Handle { get; }

    internal uint QueueFamily { get; }

    internal VulkanBindlessTables Bindless { get; }

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
        ImageUsageFlags extraUsage = 0;
        if (!initialData.IsEmpty)
        {
            extraUsage = ImageUsageFlags.TransferDstBit;
        }

        VulkanTexture texture = new(this, description, extraUsage);
        if (!initialData.IsEmpty)
        {
            UploadTexture(texture, initialData);
        }

        return texture;
    }

    public ISampler CreateSampler(in SamplerDescription description) => new VulkanSampler(this, description);

    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description) => new VulkanPipeline(this, description);

    public ICommandList CreateCommandList() => new VulkanCommandList(this);

    public void Submit(ICommandList commandList)
    {
        VulkanCommandList vulkanCommands = (VulkanCommandList)commandList;
        _submitted++;
        CommandBufferSubmitInfo commandInfo = new()
        {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = vulkanCommands.Handle,
        };
        SemaphoreSubmitInfo signal = new()
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = _timeline,
            Value = _submitted,
            StageMask = PipelineStageFlags2.AllCommandsBit,
        };
        SubmitInfo2 submit = new()
        {
            SType = StructureType.SubmitInfo2,
            CommandBufferInfoCount = 1,
            PCommandBufferInfos = &commandInfo,
            SignalSemaphoreInfoCount = 1,
            PSignalSemaphoreInfos = &signal,
        };
        VulkanResult.Check(Vk.QueueSubmit2(_queue, 1, &submit, default));
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

        Bindless.Dispose();
        Vk.DestroySemaphore(Handle, _timeline, null);
        Vk.DestroyDevice(Handle, null);
        if (_debugUtils is not null)
        {
            _debugUtils.DestroyDebugUtilsMessenger(_instance, _messenger, null);
            _debugUtils.Dispose();
        }

        Vk.DestroyInstance(_instance, null);
        Vk.Dispose();
        _self.Free();
    }

    /// <summary>Runs the release once the GPU has finished everything submitted so far.</summary>
    internal void ReleaseLater(Action release)
    {
        _releases.Enqueue((_submitted, release));
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

    internal DeviceMemory AllocateMemory(in MemoryRequirements requirements, MemoryPropertyFlags properties)
    {
        uint typeIndex = FindMemoryType(requirements.MemoryTypeBits, properties);
        MemoryAllocateInfo info = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = typeIndex,
        };
        VulkanResult.Check(Vk.AllocateMemory(Handle, &info, null, out DeviceMemory memory));
        return memory;
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

    private Instance CreateInstance(bool validation)
    {
        byte[] engineName = "FlickerVoid"u8.ToArray();
        List<string> layers = [];
        List<string> extensions = [];
        if (validation)
        {
            layers.Add(ValidationLayer);
            extensions.Add(ExtDebugUtils.ExtensionName);
        }

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
        VulkanResult.Check(debugUtils.CreateDebugUtilsMessenger(_instance, &info, null, out DebugUtilsMessengerEXT messenger));
        return messenger;
    }

    /// <summary>Picks a Vulkan 1.3 GPU with the features the engine needs and a graphics queue, preferring discrete, then integrated GPUs.</summary>
    private (PhysicalDevice Device, uint QueueFamily) SelectPhysicalDevice()
    {
        uint count = 0;
        VulkanResult.Check(Vk.EnumeratePhysicalDevices(_instance, &count, null));
        PhysicalDevice[] devices = new PhysicalDevice[count];
        fixed (PhysicalDevice* pointer = devices)
        {
            VulkanResult.Check(Vk.EnumeratePhysicalDevices(_instance, &count, pointer));
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
            && features12.ShaderSampledImageArrayNonUniformIndexing && features12.ShaderStorageBufferArrayNonUniformIndexing;
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

    private Device CreateDevice()
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
            ShaderSampledImageArrayNonUniformIndexing = true,
            ShaderStorageBufferArrayNonUniformIndexing = true,
        };
        PhysicalDeviceFeatures2 features = new() { SType = StructureType.PhysicalDeviceFeatures2, PNext = &features12 };
        DeviceCreateInfo info = new()
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &features,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queue,
        };
        VulkanResult.Check(Vk.CreateDevice(_physicalDevice, &info, null, out Device device));
        return device;
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

    private PhysicalDeviceVulkan12Properties GetVulkan12Properties()
    {
        PhysicalDeviceVulkan12Properties properties12 = new() { SType = StructureType.PhysicalDeviceVulkan12Properties };
        PhysicalDeviceProperties2 properties = new() { SType = StructureType.PhysicalDeviceProperties2, PNext = &properties12 };
        Vk.GetPhysicalDeviceProperties2(_physicalDevice, &properties);
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

    /// <summary>Copies tightly packed rows through a staging buffer with padded rows and waits, leaving shader-readable textures in ShaderRead.</summary>
    private void UploadTexture(VulkanTexture texture, ReadOnlySpan<byte> data)
    {
        TextureDescription description = texture.Description;
        int rowSize = description.Format.GetBytesPerPixel() * description.Width;
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        if (data.Length != rowSize * description.Height)
        {
            throw new ArgumentException($"Expected {rowSize * description.Height} bytes of tightly packed rows, got {data.Length}.", nameof(data));
        }

        BufferDescription stagingDescription = new((long)pitch * description.Height, BufferUsage.CopySource, MemoryLocation.Upload);
        using VulkanBuffer staging = new(this, stagingDescription, 0);
        for (int y = 0; y < description.Height; y++)
        {
            ReadOnlySpan<byte> row = data.Slice(y * rowSize, rowSize);
            staging.Write(row, (long)y * pitch);
        }

        _uploadCommands.Begin();
        _uploadCommands.Barrier(texture, ResourceState.CopyDestination);
        _uploadCommands.CopyBufferToTexture(staging, 0, texture);
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
    }
}
