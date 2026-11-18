using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A flip-model DXGI swapchain on the device's queue, presenting with vsync.</summary>
internal sealed unsafe class D3D12Swapchain : ISwapchain
{
    private const uint BufferCount = 3;

    private readonly D3D12GraphicsDevice _device;
    private readonly IDXGISwapChain3* _handle;
    private readonly string? _name;
    private D3D12Texture[] _textures = [];
    private D3D12Texture? _acquired;
    private bool _disposed;

    public D3D12Swapchain(D3D12GraphicsDevice device, in SwapchainDescription description)
    {
        if (description.Window.Win32 is not (nint hwnd, _, _))
        {
            throw new GraphicsUnavailableException($"Direct3D 12 presents to Win32 windows only, not {description.Window.Kind}.");
        }

        _device = device;
        _name = description.Name;
        SwapChainDesc1 desc = new()
        {
            Width = (uint)description.Width,
            Height = (uint)description.Height,
            Format = Silk.NET.DXGI.Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            BufferUsage = DXGI.UsageRenderTargetOutput,
            BufferCount = BufferCount,
            SwapEffect = SwapEffect.FlipDiscard,
        };
        IDXGISwapChain1* swapchain1 = null;
        D3D12Com.Check(device.Factory->CreateSwapChainForHwnd((IUnknown*)device.Queue, hwnd, &desc, null, null, &swapchain1));
        Guid swapchainId = IDXGISwapChain3.Guid;
        IDXGISwapChain3* swapchain = null;
        int result = swapchain1->QueryInterface(&swapchainId, (void**)&swapchain);
        D3D12Com.Release(swapchain1);
        D3D12Com.Check(result);
        _handle = swapchain;

        Width = description.Width;
        Height = description.Height;
        CreateTextures();
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public PixelFormat Format => PixelFormat.Bgra8UNorm;

    public ITexture AcquireNextTexture()
    {
        uint index = _handle->GetCurrentBackBufferIndex();
        D3D12Texture texture = _textures[index];
        texture.State = ResourceState.Undefined;
        _acquired = texture;
        return texture;
    }

    public void Present()
    {
        if (_acquired is null)
        {
            throw new InvalidOperationException("Acquire a back buffer before presenting.");
        }

        if (_acquired.State != ResourceState.Present)
        {
            throw new InvalidOperationException($"The back buffer must be moved to the Present state before presenting; it is in {_acquired.State}.");
        }

        _acquired = null;
        int result = _handle->Present(1, 0);
        _device.ReportMessages();
        D3D12Com.Check(result);
    }

    public void Resize(int width, int height)
    {
        ReleaseTextures();
        D3D12Com.Check(_handle->ResizeBuffers(0, (uint)width, (uint)height, Silk.NET.DXGI.Format.FormatUnknown, 0));
        Width = width;
        Height = height;
        CreateTextures();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseTextures();
        D3D12Com.Release(_handle);
    }

    private void CreateTextures()
    {
        TextureDescription description = new(Width, Height, Format, TextureUsage.RenderTarget | TextureUsage.CopySource) { Name = _name };
        Guid resourceId = ID3D12Resource.Guid;
        _textures = new D3D12Texture[BufferCount];
        for (uint index = 0; index < BufferCount; index++)
        {
            ID3D12Resource* buffer = null;
            D3D12Com.Check(_handle->GetBuffer(index, &resourceId, (void**)&buffer));
            _textures[index] = new D3D12Texture(_device, description, buffer);
        }
    }

    /// <summary>Waits for the GPU and releases the back buffers now, since DXGI can only resize or destroy buffers nothing references.</summary>
    private void ReleaseTextures()
    {
        _device.WaitIdle();
        foreach (D3D12Texture texture in _textures)
        {
            texture.Dispose();
        }

        _device.WaitIdle();
        _textures = [];
        _acquired = null;
    }
}
