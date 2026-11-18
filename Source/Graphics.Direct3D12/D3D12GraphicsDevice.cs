using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Feature = Silk.NET.Direct3D12.Feature;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>
/// A Direct3D 12 device: one direct queue, a fence counting submissions, bindless tables, and resources released once
/// the GPU has finished with them. Requires resource binding tier 3, shader model 6.0 and enhanced barriers; falls back
/// to WARP, the software rasterizer, when no GPU has them.
/// </summary>
public sealed unsafe class D3D12GraphicsDevice : IGraphicsDevice
{
    /// <summary>D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING: views return their channels in order.</summary>
    internal const uint DefaultComponentMapping = 0x1688;

    private const int RenderTargetViewCapacity = 1024;
    private const int DepthStencilViewCapacity = 256;

    private readonly ILogger _logger;
    private readonly D3D12 _d3d12;
    private readonly DXGI _dxgi;
    private readonly D3D12Runtime _runtime;
    private readonly IDXGIFactory6* _factory;
    private readonly ID3D12InfoQueue* _infoQueue;
    private readonly ID3D12CommandQueue* _queue;
    private readonly ID3D12Fence* _fence;
    private readonly Queue<(ulong Value, Action Release)> _releases = new();
    private readonly D3D12CommandList _uploadCommands;
    private ulong _submitted;
    private bool _disposed;

    public D3D12GraphicsDevice(GraphicsDeviceOptions options, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<D3D12GraphicsDevice>();
        if (!OperatingSystem.IsWindows())
        {
            throw new GraphicsUnavailableException("Direct3D 12 runs on Windows only.");
        }

        _d3d12 = D3D12.GetApi();
        _dxgi = DXGI.GetApi(null);
        _runtime = new D3D12Runtime(_d3d12, _logger);
        if (options.Validation)
        {
            EnableDebugLayer();
        }

        _factory = CreateFactory();
        Handle = CreateDevice(out string adapterName);
        AdapterName = adapterName;
        if (options.Validation)
        {
            _infoQueue = GetInfoQueue();
        }

        _queue = CreateQueue();
        _fence = CreateFence();
        Bindless = new D3D12BindlessTables(_d3d12, Handle);
        RenderTargetViews = new D3D12DescriptorHeap(Handle, DescriptorHeapType.Rtv, RenderTargetViewCapacity, shaderVisible: false);
        DepthStencilViews = new D3D12DescriptorHeap(Handle, DescriptorHeapType.Dsv, DepthStencilViewCapacity, shaderVisible: false);
        _uploadCommands = new D3D12CommandList(this);
        CreatePlaceholders();
        ReportMessages();
        _logger.LogInformation("Direct3D 12 device on {Adapter}.", AdapterName);
    }

    public GraphicsApi Api => GraphicsApi.Direct3D12;

    public string AdapterName { get; }

    internal ID3D12Device4* Handle { get; }

    internal IDXGIFactory6* Factory => _factory;

    internal ID3D12CommandQueue* Queue => _queue;

    internal D3D12BindlessTables Bindless { get; }

    internal D3D12DescriptorHeap RenderTargetViews { get; }

    internal D3D12DescriptorHeap DepthStencilViews { get; }

    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default)
    {
        D3D12Buffer buffer = new(this, description);
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
        D3D12Texture texture = new(this, description);
        if (!initialData.IsEmpty)
        {
            UploadTexture(texture, initialData);
        }

        return texture;
    }

    public ISampler CreateSampler(in SamplerDescription description) => new D3D12Sampler(this, description);

    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description) => new D3D12Pipeline(this, description);

    public ICommandList CreateCommandList() => new D3D12CommandList(this);

    public ISwapchain CreateSwapchain(in SwapchainDescription description) => new D3D12Swapchain(this, description);

    public void Submit(ICommandList commandList)
    {
        D3D12CommandList d3d12Commands = (D3D12CommandList)commandList;
        ID3D12CommandList* list = (ID3D12CommandList*)d3d12Commands.Handle;
        _queue->ExecuteCommandLists(1, &list);
        SignalQueue();
        d3d12Commands.SubmissionValue = _submitted;
        ReportMessages();
        RunCompletedReleases();
    }

    public void WaitIdle()
    {
        WaitFor(_submitted);
        ReportMessages();
        RunCompletedReleases();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uploadCommands.Dispose();
        WaitFor(_submitted);
        while (_releases.TryDequeue(out (ulong Value, Action Release) release))
        {
            release.Release();
        }

        DepthStencilViews.Dispose();
        RenderTargetViews.Dispose();
        Bindless.Dispose();
        ReportMessages();
        D3D12Com.Release(_fence);
        D3D12Com.Release(_queue);
        D3D12Com.Release(_infoQueue);
        D3D12Com.Release(Handle);
        D3D12Com.Release(_factory);
        _runtime.Dispose();
        _dxgi.Dispose();
        _d3d12.Dispose();
    }

    /// <summary>Runs the release once the GPU has finished everything submitted so far.</summary>
    internal void ReleaseLater(Action release)
    {
        _releases.Enqueue((_submitted, release));
    }

    /// <summary>Signals the next fence value once the queue has finished everything so far, presentation included.</summary>
    internal void SignalQueue()
    {
        _submitted++;
        D3D12Com.Check(_queue->Signal(_fence, _submitted));
    }

    internal void WaitFor(ulong value)
    {
        if (value == 0 || _fence->GetCompletedValue() >= value)
        {
            return;
        }

        // A null event makes the call block until the fence reaches the value.
        D3D12Com.Check(_fence->SetEventOnCompletion(value, null));
    }

    /// <summary>Creates a resource with its own heap; the optimized clear value makes clears to it fast, and only render targets and depth textures take one.</summary>
    internal ID3D12Resource* CreateCommittedResource(HeapType heapType, in ResourceDesc description, ResourceStates initialState, ClearValue* optimizedClear = null)
    {
        HeapProperties heap = new() { Type = heapType };
        Guid resourceId = ID3D12Resource.Guid;
        ID3D12Resource* resource = null;
        fixed (ResourceDesc* resourceDescription = &description)
        {
            int result = Handle->CreateCommittedResource(&heap, HeapFlags.None, resourceDescription, initialState, optimizedClear, &resourceId, (void**)&resource);
            ReportMessages();
            D3D12Com.Check(result);
        }

        return resource;
    }

    internal void SetName(ID3D12Object* target, string? name)
    {
        if (name is null)
        {
            return;
        }

        fixed (char* wide = name)
        {
            target->SetName(wide);
        }
    }

    /// <summary>Logs the debug layer's stored messages: errors as errors, warnings as warnings.</summary>
    internal void ReportMessages()
    {
        if (_infoQueue == null)
        {
            return;
        }

        ulong count = _infoQueue->GetNumStoredMessages();
        for (ulong index = 0; index < count; index++)
        {
            nuint length = 0;
            _infoQueue->GetMessageA(index, null, &length);
            if (length == 0)
            {
                continue;
            }

            byte[] bytes = new byte[(int)length];
            fixed (byte* pointer = bytes)
            {
                Message* message = (Message*)pointer;
                _infoQueue->GetMessageA(index, message, &length);
                string description = Marshal.PtrToStringAnsi((nint)message->PDescription) ?? string.Empty;
                LogMessage(message->Severity, message->ID, description);
            }
        }

        _infoQueue->ClearStoredMessages();
    }

    private void LogMessage(MessageSeverity severity, MessageID id, string description)
    {
        switch (severity)
        {
            case MessageSeverity.Corruption:
            case MessageSeverity.Error:
                _logger.LogError("Direct3D 12 ({Id}): {Message}", id, description);
                break;
            case MessageSeverity.Warning:
                _logger.LogWarning("Direct3D 12 ({Id}): {Message}", id, description);
                break;
        }
    }

    private void EnableDebugLayer()
    {
        Guid debugId = ID3D12Debug.Guid;
        ID3D12Debug* debug = null;
        int result = _runtime.GetDebugInterface(D3D12Runtime.DebugClassId, &debugId, (void**)&debug);
        if (result < 0)
        {
            _logger.LogWarning("The Direct3D 12 debug layer is not available (0x{Result:X8}); running without validation.", result);
            return;
        }

        debug->EnableDebugLayer();
        debug->Release();
    }

    private IDXGIFactory6* CreateFactory()
    {
        Guid factoryId = IDXGIFactory6.Guid;
        IDXGIFactory6* factory = null;
        int result = _dxgi.CreateDXGIFactory2(0, &factoryId, (void**)&factory);
        if (result < 0)
        {
            throw new GraphicsUnavailableException($"DXGI 1.6 is not available (0x{result:X8}).");
        }

        return factory;
    }

    /// <summary>Picks the fastest suitable hardware adapter, then WARP.</summary>
    private ID3D12Device4* CreateDevice(out string adapterName)
    {
        Guid adapterId = IDXGIAdapter1.Guid;
        for (uint index = 0; ; index++)
        {
            IDXGIAdapter1* adapter = null;
            if (_factory->EnumAdapterByGpuPreference(index, GpuPreference.HighPerformance, &adapterId, (void**)&adapter) < 0)
            {
                break;
            }

            AdapterDesc1 description;
            D3D12Com.Check(adapter->GetDesc1(&description));
            bool software = (description.Flags & (uint)AdapterFlag.Software) != 0;
            ID3D12Device4* device = null;
            if (!software)
            {
                device = TryCreateDevice(adapter);
            }

            adapter->Release();
            if (device != null)
            {
                adapterName = GetName(description);
                return device;
            }
        }

        IDXGIAdapter1* warp = null;
        if (((IDXGIFactory4*)_factory)->EnumWarpAdapter(&adapterId, (void**)&warp) >= 0)
        {
            AdapterDesc1 description;
            D3D12Com.Check(warp->GetDesc1(&description));
            ID3D12Device4* device = TryCreateDevice(warp);
            warp->Release();
            if (device != null)
            {
                adapterName = GetName(description);
                return device;
            }
        }

        throw new GraphicsUnavailableException("No adapter supports Direct3D 12 with resource binding tier 3, shader model 6.0 and enhanced barriers.");
    }

    private static string GetName(AdapterDesc1 description)
    {
        return SilkMarshal.PtrToString((nint)description.Description, NativeStringEncoding.LPWStr) ?? "Direct3D 12 adapter";
    }

    private ID3D12Device4* TryCreateDevice(IDXGIAdapter1* adapter)
    {
        Guid deviceId = ID3D12Device4.Guid;
        ID3D12Device4* device = null;
        if (_runtime.CreateDevice((IUnknown*)adapter, &deviceId, (void**)&device) < 0)
        {
            return null;
        }

        if (IsSuitable(device))
        {
            return device;
        }

        device->Release();
        return null;
    }

    private static bool IsSuitable(ID3D12Device4* device)
    {
        FeatureDataD3D12Options options = default;
        if (device->CheckFeatureSupport(Feature.D3D12Options, &options, (uint)sizeof(FeatureDataD3D12Options)) < 0
            || options.ResourceBindingTier < ResourceBindingTier.Tier3)
        {
            return false;
        }

        FeatureDataShaderModel shaderModel = new() { HighestShaderModel = D3DShaderModel.ShaderModel60 };
        if (device->CheckFeatureSupport(Feature.ShaderModel, &shaderModel, (uint)sizeof(FeatureDataShaderModel)) < 0
            || shaderModel.HighestShaderModel < D3DShaderModel.ShaderModel60)
        {
            return false;
        }

        // CheckFeatureSupport fails with E_INVALIDARG unless the size is exactly the struct's.
        FeatureDataD3D12Options12 options12 = default;
        return device->CheckFeatureSupport(Feature.D3D12Options12, &options12, (uint)sizeof(FeatureDataD3D12Options12)) >= 0
            && options12.EnhancedBarriersSupported;
    }

    private ID3D12InfoQueue* GetInfoQueue()
    {
        Guid infoQueueId = ID3D12InfoQueue.Guid;
        ID3D12InfoQueue* infoQueue = null;
        if (Handle->QueryInterface(&infoQueueId, (void**)&infoQueue) < 0)
        {
            return null;
        }

        return infoQueue;
    }

    private ID3D12CommandQueue* CreateQueue()
    {
        CommandQueueDesc description = new() { Type = CommandListType.Direct };
        Guid queueId = ID3D12CommandQueue.Guid;
        ID3D12CommandQueue* queue = null;
        D3D12Com.Check(Handle->CreateCommandQueue(&description, &queueId, (void**)&queue));
        return queue;
    }

    private ID3D12Fence* CreateFence()
    {
        Guid fenceId = ID3D12Fence.Guid;
        ID3D12Fence* fence = null;
        D3D12Com.Check(Handle->CreateFence(0, FenceFlags.None, &fenceId, (void**)&fence));
        return fence;
    }

    private void RunCompletedReleases()
    {
        ulong completed = _fence->GetCompletedValue();
        while (_releases.TryPeek(out (ulong Value, Action Release) release) && release.Value <= completed)
        {
            _releases.Dequeue();
            release.Release();
        }
    }

    /// <summary>Copies the data through a staging buffer and waits, leaving the buffer in the state its usage reads it in.</summary>
    private void UploadBuffer(D3D12Buffer buffer, ReadOnlySpan<byte> data)
    {
        BufferDescription stagingDescription = new(data.Length, BufferUsage.CopySource, MemoryLocation.Upload);
        using D3D12Buffer staging = new(this, stagingDescription);
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
    private void UploadTexture(D3D12Texture texture, ReadOnlySpan<byte> data)
    {
        TextureDescription description = texture.Description;
        int rowSize = description.Format.GetBytesPerPixel() * description.Width;
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        if (data.Length != rowSize * description.Height)
        {
            throw new ArgumentException($"Expected {rowSize * description.Height} bytes of tightly packed rows, got {data.Length}.", nameof(data));
        }

        BufferDescription stagingDescription = new((long)pitch * description.Height, BufferUsage.CopySource, MemoryLocation.Upload);
        using D3D12Buffer staging = new(this, stagingDescription);
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

    /// <summary>
    /// Fills slot 0 of each bindless table, so shaders reading a missing resource get zeros. Textures and buffers share
    /// the resource slot, so slot 0 holds a null texture view; reading it as a buffer is undefined.
    /// </summary>
    private void CreatePlaceholders()
    {
        ShaderResourceViewDesc nullTexture = new()
        {
            Format = Format.FormatR8G8B8A8Unorm,
            ViewDimension = SrvDimension.Texture2D,
            Shader4ComponentMapping = DefaultComponentMapping,
        };
        nullTexture.Texture2D = new Tex2DSrv { MipLevels = 1 };
        Bindless.WriteResource(BindlessIndex.None, null, nullTexture);

        SamplerDesc sampler = new()
        {
            Filter = Silk.NET.Direct3D12.Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MaxAnisotropy = 1,
            ComparisonFunc = ComparisonFunc.Never,
            MaxLOD = float.MaxValue,
        };
        Bindless.WriteSampler(BindlessIndex.None, sampler);
    }
}
