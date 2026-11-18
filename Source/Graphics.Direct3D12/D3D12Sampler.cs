using Silk.NET.Direct3D12;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A sampler in the bindless sampler heap.</summary>
internal sealed class D3D12Sampler : ISampler
{
    private readonly D3D12GraphicsDevice _device;
    private bool _disposed;

    public D3D12Sampler(D3D12GraphicsDevice device, in SamplerDescription description)
    {
        _device = device;
        Description = description;
        TextureAddressMode address = description.AddressMode.ToD3D12();
        SamplerDesc sampler = new()
        {
            Filter = description.Filter.ToD3D12(),
            AddressU = address,
            AddressV = address,
            AddressW = address,
            MaxAnisotropy = 1,
            ComparisonFunc = ComparisonFunc.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
        };
        BindlessIndex = device.Bindless.Samplers.Allocate();
        device.Bindless.WriteSampler(BindlessIndex, sampler);
    }

    public SamplerDescription Description { get; }

    public uint BindlessIndex { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        uint index = BindlessIndex;
        D3D12GraphicsDevice device = _device;
        device.ReleaseLater(() => device.Bindless.Samplers.Free(index));
    }
}
