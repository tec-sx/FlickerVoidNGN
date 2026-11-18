using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A committed 2D Direct3D 12 texture with the views its usage needs.</summary>
internal sealed unsafe class D3D12Texture : ITexture
{
    private readonly D3D12GraphicsDevice _device;
    private readonly uint _renderTargetSlot;
    private readonly uint _depthStencilSlot;
    private bool _disposed;

    public D3D12Texture(D3D12GraphicsDevice device, in TextureDescription description)
        : this(device, description, CreateResource(device, description))
    {
    }

    /// <summary>Wraps a resource something else created, such as a swapchain back buffer, taking over one reference to it.</summary>
    public D3D12Texture(D3D12GraphicsDevice device, in TextureDescription description, ID3D12Resource* resource)
    {
        _device = device;
        Description = description;
        bool shaderRead = description.Usage.HasFlag(TextureUsage.ShaderRead);
        ResourceFormat = description.Format.ToResourceFormat(shaderRead);
        Handle = resource;

        if (shaderRead)
        {
            BindlessIndex = device.Bindless.Resources.Allocate();
            ShaderResourceViewDesc view = new()
            {
                Format = description.Format.ToShaderReadFormat(),
                ViewDimension = SrvDimension.Texture2D,
                Shader4ComponentMapping = D3D12GraphicsDevice.DefaultComponentMapping,
            };
            view.Texture2D = new Tex2DSrv { MipLevels = 1 };
            device.Bindless.WriteResource(BindlessIndex, Handle, view);
        }

        if (description.Usage.HasFlag(TextureUsage.RenderTarget))
        {
            _renderTargetSlot = device.RenderTargetViews.Slots.Allocate();
            RenderTargetView = device.RenderTargetViews.GetCpuHandle(_renderTargetSlot);
            RenderTargetViewDesc view = new() { Format = description.Format.ToDxgi(), ViewDimension = RtvDimension.Texture2D };
            device.Handle->CreateRenderTargetView(Handle, &view, RenderTargetView);
        }

        if (description.Usage.HasFlag(TextureUsage.DepthStencil))
        {
            _depthStencilSlot = device.DepthStencilViews.Slots.Allocate();
            DepthStencilView = device.DepthStencilViews.GetCpuHandle(_depthStencilSlot);
            DepthStencilViewDesc view = new() { Format = description.Format.ToDxgi(), ViewDimension = DsvDimension.Texture2D };
            device.Handle->CreateDepthStencilView(Handle, &view, DepthStencilView);
        }

        device.SetName((ID3D12Object*)Handle, description.Name);
    }

    public ID3D12Resource* Handle { get; }

    /// <summary>The format the resource was created with; typeless for depth textures shaders read.</summary>
    public Format ResourceFormat { get; }

    public CpuDescriptorHandle RenderTargetView { get; }

    public CpuDescriptorHandle DepthStencilView { get; }

    public TextureDescription Description { get; }

    public uint BindlessIndex { get; }

    /// <summary>The state the last recorded barrier left it in.</summary>
    public ResourceState State { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ID3D12Resource* handle = Handle;
        uint index = BindlessIndex;
        uint renderTargetSlot = _renderTargetSlot;
        uint depthStencilSlot = _depthStencilSlot;
        D3D12GraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            D3D12Com.Release(handle);
            device.Bindless.Resources.Free(index);
            device.RenderTargetViews.Slots.Free(renderTargetSlot);
            device.DepthStencilViews.Slots.Free(depthStencilSlot);
        });
    }

    private static ID3D12Resource* CreateResource(D3D12GraphicsDevice device, in TextureDescription description)
    {
        bool shaderRead = description.Usage.HasFlag(TextureUsage.ShaderRead);
        ResourceDesc resource = new()
        {
            Dimension = ResourceDimension.Texture2D,
            Width = (ulong)description.Width,
            Height = (uint)description.Height,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = description.Format.ToResourceFormat(shaderRead),
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = GetFlags(description.Usage),
        };
        if (!description.Usage.HasFlag(TextureUsage.DepthStencil))
        {
            return device.CreateCommittedResource(HeapType.Default, resource, ResourceStates.Common);
        }

        // Depth clears to 1, the far plane, so that is the value the hardware can clear fastest.
        ClearValue clear = new(description.Format.ToDxgi(), depthStencil: new DepthStencilValue(1, 0));
        return device.CreateCommittedResource(HeapType.Default, resource, ResourceStates.Common, &clear);
    }

    private static ResourceFlags GetFlags(TextureUsage usage)
    {
        ResourceFlags flags = ResourceFlags.None;
        if (usage.HasFlag(TextureUsage.RenderTarget))
        {
            flags |= ResourceFlags.AllowRenderTarget;
        }

        if (usage.HasFlag(TextureUsage.DepthStencil))
        {
            flags |= ResourceFlags.AllowDepthStencil;
            if (!usage.HasFlag(TextureUsage.ShaderRead))
            {
                flags |= ResourceFlags.DenyShaderResource;
            }
        }

        return flags;
    }
}
