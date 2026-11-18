using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.Maths;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A direct command list with its own allocator, reset on Begin once the GPU has finished its previous submission.</summary>
internal sealed unsafe class D3D12CommandList : ICommandList
{
    private readonly D3D12GraphicsDevice _device;
    private readonly ID3D12CommandAllocator* _allocator;
    private bool _disposed;

    public D3D12CommandList(D3D12GraphicsDevice device)
    {
        _device = device;
        Guid allocatorId = ID3D12CommandAllocator.Guid;
        ID3D12CommandAllocator* allocator = null;
        D3D12Com.Check(device.Handle->CreateCommandAllocator(CommandListType.Direct, &allocatorId, (void**)&allocator));
        _allocator = allocator;

        // Created closed, so every use starts with a reset.
        Guid listId = ID3D12GraphicsCommandList7.Guid;
        ID3D12GraphicsCommandList7* list = null;
        D3D12Com.Check(device.Handle->CreateCommandList1(0, CommandListType.Direct, CommandListFlags.None, &listId, (void**)&list));
        Handle = list;
    }

    public ID3D12GraphicsCommandList7* Handle { get; }

    /// <summary>The device fence value its last submission signals; 0 before the first.</summary>
    public ulong SubmissionValue { get; set; }

    public void Begin()
    {
        _device.WaitFor(SubmissionValue);
        D3D12Com.Check(_allocator->Reset());
        D3D12Com.Check(Handle->Reset(_allocator, null));
        _device.Bindless.Bind(Handle);
    }

    public void End()
    {
        D3D12Com.Check(Handle->Close());
    }

    public void Barrier(ITexture texture, ResourceState state)
    {
        D3D12Texture d3d12Texture = (D3D12Texture)texture;
        D3D12ResourceStates before = D3D12ResourceStates.For(d3d12Texture.State);
        D3D12ResourceStates after = D3D12ResourceStates.For(state);
        TextureBarrier barrier = new()
        {
            SyncBefore = before.Sync,
            SyncAfter = after.Sync,
            AccessBefore = before.Access,
            AccessAfter = after.Access,
            LayoutBefore = before.Layout,
            LayoutAfter = after.Layout,
            PResource = d3d12Texture.Handle,
            Subresources = new BarrierSubresourceRange { IndexOrFirstMipLevel = uint.MaxValue },
            Flags = TextureBarrierFlags.None,
        };
        BarrierGroup group = new()
        {
            Type = BarrierType.Texture,
            NumBarriers = 1,
        };
        group.PTextureBarriers = &barrier;
        Handle->Barrier(1, &group);
        d3d12Texture.State = state;
    }

    public void Barrier(IBuffer buffer, ResourceState state)
    {
        D3D12Buffer d3d12Buffer = (D3D12Buffer)buffer;
        D3D12ResourceStates before = D3D12ResourceStates.For(d3d12Buffer.State);
        D3D12ResourceStates after = D3D12ResourceStates.For(state);
        BufferBarrier barrier = new()
        {
            SyncBefore = before.Sync,
            SyncAfter = after.Sync,
            AccessBefore = before.Access,
            AccessAfter = after.Access,
            PResource = d3d12Buffer.Handle,
            Offset = 0,
            Size = ulong.MaxValue,
        };
        BarrierGroup group = new()
        {
            Type = BarrierType.Buffer,
            NumBarriers = 1,
        };
        group.PBufferBarriers = &barrier;
        Handle->Barrier(1, &group);
        d3d12Buffer.State = state;
    }

    public void CopyBuffer(IBuffer source, long sourceOffset, IBuffer destination, long destinationOffset, long size)
    {
        D3D12Buffer from = (D3D12Buffer)source;
        D3D12Buffer to = (D3D12Buffer)destination;
        Handle->CopyBufferRegion(to.Handle, (ulong)destinationOffset, from.Handle, (ulong)sourceOffset, (ulong)size);
    }

    public void CopyBufferToTexture(IBuffer source, long sourceOffset, ITexture destination)
    {
        D3D12Buffer from = (D3D12Buffer)source;
        D3D12Texture to = (D3D12Texture)destination;
        TextureCopyLocation footprint = CreateFootprint(from, sourceOffset, to);
        TextureCopyLocation subresource = CreateSubresource(to);
        Handle->CopyTextureRegion(&subresource, 0, 0, 0, &footprint, null);
    }

    public void CopyTextureToBuffer(ITexture source, IBuffer destination, long destinationOffset)
    {
        D3D12Texture from = (D3D12Texture)source;
        D3D12Buffer to = (D3D12Buffer)destination;
        TextureCopyLocation footprint = CreateFootprint(to, destinationOffset, from);
        TextureCopyLocation subresource = CreateSubresource(from);
        Handle->CopyTextureRegion(&footprint, 0, 0, 0, &subresource, null);
    }

    public void BeginRendering(ReadOnlySpan<ColorTarget> colorTargets, DepthTarget? depthTarget = null)
    {
        CpuDescriptorHandle* colors = stackalloc CpuDescriptorHandle[colorTargets.Length];
        for (int index = 0; index < colorTargets.Length; index++)
        {
            D3D12Texture texture = (D3D12Texture)colorTargets[index].Texture;
            colors[index] = texture.RenderTargetView;
        }

        CpuDescriptorHandle depthView = default;
        CpuDescriptorHandle* depth = null;
        if (depthTarget is { } depthValue)
        {
            D3D12Texture texture = (D3D12Texture)depthValue.Texture;
            depthView = texture.DepthStencilView;
            depth = &depthView;
        }

        Handle->OMSetRenderTargets((uint)colorTargets.Length, colors, false, depth);

        for (int index = 0; index < colorTargets.Length; index++)
        {
            ColorTarget target = colorTargets[index];
            if (target.Load == LoadAction.Clear)
            {
                Vector4 color = target.ClearColor;
                Handle->ClearRenderTargetView(colors[index], (float*)&color, 0, null);
            }
        }

        if (depthTarget is { Load: LoadAction.Clear } clearedDepth)
        {
            ClearFlags flags = ClearFlags.Depth;
            if (clearedDepth.Texture.Description.Format.HasStencil())
            {
                flags |= ClearFlags.Stencil;
            }

            Handle->ClearDepthStencilView(depthView, flags, clearedDepth.ClearDepth, 0, 0, null);
        }
    }

    public void EndRendering()
    {
        // Direct3D 12 has no render pass to end here; render targets stay bound until the next BeginRendering.
    }

    public void SetViewport(in Viewport viewport)
    {
        Silk.NET.Direct3D12.Viewport d3d12Viewport = new()
        {
            TopLeftX = viewport.X,
            TopLeftY = viewport.Y,
            Width = viewport.Width,
            Height = viewport.Height,
            MinDepth = viewport.MinDepth,
            MaxDepth = viewport.MaxDepth,
        };
        Handle->RSSetViewports(1, &d3d12Viewport);
    }

    public void SetScissor(int x, int y, int width, int height)
    {
        Box2D<int> scissor = new(x, y, x + width, y + height);
        Handle->RSSetScissorRects(1, &scissor);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        D3D12Pipeline d3d12Pipeline = (D3D12Pipeline)pipeline;
        Handle->SetPipelineState(d3d12Pipeline.Handle);
        Handle->IASetPrimitiveTopology(d3d12Pipeline.Topology);
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, long offset = 0)
    {
        D3D12Buffer d3d12Buffer = (D3D12Buffer)buffer;
        IndexBufferView view = new()
        {
            BufferLocation = d3d12Buffer.Handle->GetGPUVirtualAddress() + (ulong)offset,
            SizeInBytes = (uint)(d3d12Buffer.Description.Size - offset),
            Format = format.ToDxgi(),
        };
        Handle->IASetIndexBuffer(&view);
    }

    public void PushConstants<T>(in T data)
        where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        if (size > GraphicsLimits.PushConstantSize || size % sizeof(uint) != 0)
        {
            throw new ArgumentException($"Push constants are a multiple of 4 bytes, at most {GraphicsLimits.PushConstantSize}; {typeof(T).Name} has {size}.", nameof(data));
        }

        fixed (T* pointer = &data)
        {
            _device.Bindless.PushConstants(Handle, pointer, size);
        }
    }

    public void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        Handle->DrawInstanced((uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    public void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
    {
        Handle->DrawIndexedInstanced((uint)indexCount, (uint)instanceCount, (uint)firstIndex, vertexOffset, (uint)firstInstance);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ID3D12GraphicsCommandList7* list = Handle;
        ID3D12CommandAllocator* allocator = _allocator;
        _device.ReleaseLater(() =>
        {
            D3D12Com.Release(list);
            D3D12Com.Release(allocator);
        });
    }

    private static TextureCopyLocation CreateSubresource(D3D12Texture texture)
    {
        TextureCopyLocation location = new()
        {
            PResource = texture.Handle,
            Type = TextureCopyType.SubresourceIndex,
        };
        location.SubresourceIndex = 0;
        return location;
    }

    /// <summary>The texture's rows in the buffer, padded to <see cref="PixelFormatExtensions.GetCopyRowPitch"/>; the offset must be a multiple of 512.</summary>
    private static TextureCopyLocation CreateFootprint(D3D12Buffer buffer, long offset, D3D12Texture texture)
    {
        TextureDescription description = texture.Description;
        TextureCopyLocation location = new()
        {
            PResource = buffer.Handle,
            Type = TextureCopyType.PlacedFootprint,
        };
        location.PlacedFootprint = new PlacedSubresourceFootprint
        {
            Offset = (ulong)offset,
            Footprint = new SubresourceFootprint
            {
                Format = texture.ResourceFormat,
                Width = (uint)description.Width,
                Height = (uint)description.Height,
                Depth = 1,
                RowPitch = (uint)description.Format.GetCopyRowPitch(description.Width),
            },
        };
        return location;
    }
}
