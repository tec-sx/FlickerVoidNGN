using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>
/// The shader-visible descriptor heaps shaders index into, and the one root signature every pipeline uses:
/// parameter 0 holds the push constants (b0, space0); parameters 1 and 2 are tables over the whole resource heap
/// for Texture2D[] (t0, space1) and ByteAddressBuffer[] (t0, space2), so a slot reads as either; parameter 3 is
/// the sampler heap for SamplerState[] (s0, space3).
/// </summary>
internal sealed unsafe class D3D12BindlessTables : IDisposable
{
    private const uint PushConstantParameter = 0;
    private const uint TextureParameter = 1;
    private const uint BufferParameter = 2;
    private const uint SamplerParameter = 3;

    /// <summary>The most samplers a shader-visible heap can hold.</summary>
    private const int MaxSamplerHeapSize = 2048;

    private readonly ID3D12Device4* _device;
    private readonly D3D12DescriptorHeap _resources;
    private readonly D3D12DescriptorHeap _samplers;

    public D3D12BindlessTables(D3D12 d3d12, ID3D12Device4* device)
    {
        _device = device;
        _resources = new D3D12DescriptorHeap(device, DescriptorHeapType.CbvSrvUav, GraphicsLimits.BindlessResourceCapacity, shaderVisible: true);
        int samplerCapacity = Math.Min(GraphicsLimits.BindlessSamplerCapacity, MaxSamplerHeapSize);
        _samplers = new D3D12DescriptorHeap(device, DescriptorHeapType.Sampler, samplerCapacity, shaderVisible: true);
        RootSignature = CreateRootSignature(d3d12);
    }

    /// <summary>Slots shared by textures and buffers.</summary>
    public BindlessIndexAllocator Resources => _resources.Slots;

    public BindlessIndexAllocator Samplers => _samplers.Slots;

    public ID3D12RootSignature* RootSignature { get; }

    public void Bind(ID3D12GraphicsCommandList7* commandList)
    {
        ID3D12DescriptorHeap** heaps = stackalloc ID3D12DescriptorHeap*[2];
        heaps[0] = _resources.Heap;
        heaps[1] = _samplers.Heap;
        commandList->SetDescriptorHeaps(2, heaps);
        commandList->SetGraphicsRootSignature(RootSignature);
        commandList->SetGraphicsRootDescriptorTable(TextureParameter, _resources.GpuStart);
        commandList->SetGraphicsRootDescriptorTable(BufferParameter, _resources.GpuStart);
        commandList->SetGraphicsRootDescriptorTable(SamplerParameter, _samplers.GpuStart);
    }

    public void PushConstants(ID3D12GraphicsCommandList7* commandList, void* data, int size)
    {
        commandList->SetGraphicsRoot32BitConstants(PushConstantParameter, (uint)(size / sizeof(uint)), data, 0);
    }

    /// <summary>Writes a shader resource view of the resource (a null view when it is null) to the slot.</summary>
    public void WriteResource(uint index, ID3D12Resource* resource, in ShaderResourceViewDesc view)
    {
        CpuDescriptorHandle handle = _resources.GetCpuHandle(index);
        fixed (ShaderResourceViewDesc* description = &view)
        {
            _device->CreateShaderResourceView(resource, description, handle);
        }
    }

    public void WriteSampler(uint index, in SamplerDesc sampler)
    {
        CpuDescriptorHandle handle = _samplers.GetCpuHandle(index);
        fixed (SamplerDesc* description = &sampler)
        {
            _device->CreateSampler(description, handle);
        }
    }

    public void Dispose()
    {
        D3D12Com.Release(RootSignature);
        _samplers.Dispose();
        _resources.Dispose();
    }

    private ID3D12RootSignature* CreateRootSignature(D3D12 d3d12)
    {
        // Volatile descriptors: only the slots a shader reads must hold a view, and slots change while command lists are in flight.
        const DescriptorRangeFlags volatileFlags = DescriptorRangeFlags.DescriptorsVolatile | DescriptorRangeFlags.DataVolatile;
        DescriptorRange1* ranges = stackalloc DescriptorRange1[3];
        ranges[0] = CreateUnboundedRange(DescriptorRangeType.Srv, 1, volatileFlags);
        ranges[1] = CreateUnboundedRange(DescriptorRangeType.Srv, 2, volatileFlags);
        ranges[2] = CreateUnboundedRange(DescriptorRangeType.Sampler, 3, DescriptorRangeFlags.DescriptorsVolatile);

        RootParameter1* parameters = stackalloc RootParameter1[4];
        parameters[PushConstantParameter] = new RootParameter1
        {
            ParameterType = RootParameterType.Type32BitConstants,
            ShaderVisibility = ShaderVisibility.All,
        };
        parameters[PushConstantParameter].Constants = new RootConstants
        {
            ShaderRegister = 0,
            RegisterSpace = 0,
            Num32BitValues = GraphicsLimits.PushConstantSize / sizeof(uint),
        };
        parameters[TextureParameter] = CreateTable(&ranges[0]);
        parameters[BufferParameter] = CreateTable(&ranges[1]);
        parameters[SamplerParameter] = CreateTable(&ranges[2]);

        VersionedRootSignatureDesc description = new() { Version = D3DRootSignatureVersion.Version11 };
        description.Desc11 = new RootSignatureDesc1
        {
            NumParameters = 4,
            PParameters = parameters,
            Flags = RootSignatureFlags.None,
        };

        ID3D10Blob* blob = null;
        ID3D10Blob* errors = null;
        try
        {
            int result = Serialize(d3d12, &description, &blob, &errors);
            if (result < 0)
            {
                string message = string.Empty;
                if (errors != null)
                {
                    message = SilkMarshal.PtrToString((nint)errors->GetBufferPointer()) ?? string.Empty;
                }

                throw new InvalidOperationException($"Root signature serialization failed (0x{result:X8}): {message}");
            }

            Guid rootSignatureId = ID3D12RootSignature.Guid;
            ID3D12RootSignature* rootSignature = null;
            D3D12Com.Check(_device->CreateRootSignature(0, blob->GetBufferPointer(), blob->GetBufferSize(), &rootSignatureId, (void**)&rootSignature));
            return rootSignature;
        }
        finally
        {
            D3D12Com.Release(blob);
            D3D12Com.Release(errors);
        }
    }

    /// <summary>The system serializer belongs to the system runtime; a device from the Agility SDK serializes with its own.</summary>
    private int Serialize(D3D12 d3d12, VersionedRootSignatureDesc* description, ID3D10Blob** blob, ID3D10Blob** errors)
    {
        Guid configurationId = ID3D12DeviceConfiguration.Guid;
        ID3D12DeviceConfiguration* configuration = null;
        if (_device->QueryInterface(&configurationId, (void**)&configuration) >= 0)
        {
            int result = configuration->SerializeVersionedRootSignature(description, blob, errors);
            configuration->Release();
            return result;
        }

        return d3d12.SerializeVersionedRootSignature(description, blob, errors);
    }

    private static DescriptorRange1 CreateUnboundedRange(DescriptorRangeType type, uint space, DescriptorRangeFlags flags)
    {
        return new DescriptorRange1
        {
            RangeType = type,
            NumDescriptors = uint.MaxValue,
            BaseShaderRegister = 0,
            RegisterSpace = space,
            Flags = flags,
            OffsetInDescriptorsFromTableStart = 0,
        };
    }

    private static RootParameter1 CreateTable(DescriptorRange1* range)
    {
        RootParameter1 parameter = new()
        {
            ParameterType = RootParameterType.TypeDescriptorTable,
            ShaderVisibility = ShaderVisibility.All,
        };
        parameter.DescriptorTable = new RootDescriptorTable1 { NumDescriptorRanges = 1, PDescriptorRanges = range };
        return parameter;
    }
}
