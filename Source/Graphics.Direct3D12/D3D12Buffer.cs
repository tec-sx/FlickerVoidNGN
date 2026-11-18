using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A committed Direct3D 12 buffer; upload and readback buffers stay mapped. Shaders read it as a raw buffer.</summary>
internal sealed unsafe class D3D12Buffer : IBuffer
{
    private readonly D3D12GraphicsDevice _device;
    private readonly byte* _mapped;
    private bool _disposed;

    public D3D12Buffer(D3D12GraphicsDevice device, in BufferDescription description)
    {
        _device = device;
        Description = description;
        ResourceDesc resource = new()
        {
            Dimension = ResourceDimension.Buffer,
            Width = (ulong)description.Size,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = ResourceFlags.None,
        };
        (HeapType heap, ResourceStates initialState) = GetHeap(description.Memory);
        Handle = device.CreateCommittedResource(heap, resource, initialState);

        if (description.Memory != MemoryLocation.Gpu)
        {
            // Upload buffers are never read by the CPU: an empty read range.
            Silk.NET.Direct3D12.Range noRead = default;
            Silk.NET.Direct3D12.Range* readRange = null;
            if (description.Memory == MemoryLocation.Upload)
            {
                readRange = &noRead;
            }

            void* mapped;
            D3D12Com.Check(Handle->Map(0, readRange, &mapped));
            _mapped = (byte*)mapped;
        }

        if (description.Usage.HasFlag(BufferUsage.ShaderRead))
        {
            BindlessIndex = device.Bindless.Resources.Allocate();
            ShaderResourceViewDesc view = new()
            {
                Format = Format.FormatR32Typeless,
                ViewDimension = SrvDimension.Buffer,
                Shader4ComponentMapping = D3D12GraphicsDevice.DefaultComponentMapping,
            };
            view.Buffer = new BufferSrv
            {
                FirstElement = 0,
                NumElements = (uint)(description.Size / sizeof(uint)),
                Flags = BufferSrvFlags.Raw,
            };
            device.Bindless.WriteResource(BindlessIndex, Handle, view);
        }

        device.SetName((ID3D12Object*)Handle, description.Name);
    }

    public ID3D12Resource* Handle { get; }

    public BufferDescription Description { get; }

    public uint BindlessIndex { get; }

    /// <summary>The state the last recorded barrier left it in.</summary>
    public ResourceState State { get; set; }

    public void Write(ReadOnlySpan<byte> data, long offset = 0)
    {
        RequireMapped(MemoryLocation.Upload, data.Length, offset);
        Span<byte> destination = new(_mapped + offset, data.Length);
        data.CopyTo(destination);
    }

    public void Read(Span<byte> data, long offset = 0)
    {
        RequireMapped(MemoryLocation.Readback, data.Length, offset);
        ReadOnlySpan<byte> source = new(_mapped + offset, data.Length);
        source.CopyTo(data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ID3D12Resource* handle = Handle;
        uint index = BindlessIndex;
        D3D12GraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            D3D12Com.Release(handle);
            device.Bindless.Resources.Free(index);
        });
    }

    private static (HeapType Heap, ResourceStates InitialState) GetHeap(MemoryLocation location)
    {
        switch (location)
        {
            case MemoryLocation.Gpu:
                return (HeapType.Default, ResourceStates.Common);
            case MemoryLocation.Upload:
                return (HeapType.Upload, ResourceStates.GenericRead);
            case MemoryLocation.Readback:
                return (HeapType.Readback, ResourceStates.CopyDest);
            default:
                throw new ArgumentOutOfRangeException(nameof(location), location, null);
        }
    }

    private void RequireMapped(MemoryLocation location, int length, long offset)
    {
        if (Description.Memory != location)
        {
            throw new InvalidOperationException($"Only {location} buffers can do that; this one is {Description.Memory}.");
        }

        if (offset < 0 || offset + length > Description.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), $"{length} bytes at {offset} don't fit in {Description.Size}.");
        }
    }
}
