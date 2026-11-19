using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.Buffers;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Pipelines;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;

using Viewport = FlickerVoid.Engine.Graphics.Commands.Viewport;

namespace FlickerVoid.Engine.Graphics.Vulkan.Commands;

/// <summary>A command buffer with its own pool, reset on Begin once the GPU has finished its previous submission.</summary>
internal sealed unsafe class VulkanCommandList : ICommandList
{
    private readonly VulkanGraphicsDevice _device;
    private readonly Vk _vk;
    private readonly CommandPool _pool;
    private bool _disposed;

    public VulkanCommandList(VulkanGraphicsDevice device)
    {
        _device = device;
        _vk = device.Vk;
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = device.QueueFamily,
        };
        VulkanResult.Check(_vk.CreateCommandPool(device.Handle, &poolInfo, null, out CommandPool pool));
        _pool = pool;

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = pool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };
        VulkanResult.Check(_vk.AllocateCommandBuffers(device.Handle, &allocateInfo, out CommandBuffer buffer));
        Handle = buffer;
    }

    public CommandBuffer Handle { get; }

    /// <summary>The device timeline value its last submission signals; 0 before the first.</summary>
    public ulong SubmissionValue { get; set; }

    public void Begin()
    {
        _device.WaitFor(SubmissionValue);
        VulkanResult.Check(_vk.ResetCommandPool(_device.Handle, _pool, 0));
        CommandBufferBeginInfo info = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        VulkanResult.Check(_vk.BeginCommandBuffer(Handle, &info));
        _device.Bindless.Bind(Handle, PipelineBindPoint.Graphics);
        _device.Bindless.Bind(Handle, PipelineBindPoint.Compute);
    }

    public void End()
    {
        VulkanResult.Check(_vk.EndCommandBuffer(Handle));
    }

    public void Barrier(ITexture texture, ResourceState state)
    {
        VulkanTexture vulkanTexture = (VulkanTexture)texture;
        VulkanResourceStates before = VulkanResourceStates.For(vulkanTexture.State);
        VulkanResourceStates after = VulkanResourceStates.For(state);
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = before.Stages,
            SrcAccessMask = before.Access,
            DstStageMask = after.Stages,
            DstAccessMask = after.Access,
            OldLayout = before.Layout,
            NewLayout = after.Layout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = vulkanTexture.Handle,
            SubresourceRange = new ImageSubresourceRange(vulkanTexture.Aspect, 0, (uint)vulkanTexture.Description.MipLevels, 0, 1),
        };
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };
        _vk.CmdPipelineBarrier2(Handle, &dependency);
        vulkanTexture.State = state;
    }

    public void Barrier(IBuffer buffer, ResourceState state)
    {
        VulkanBuffer vulkanBuffer = (VulkanBuffer)buffer;
        VulkanResourceStates before = VulkanResourceStates.For(vulkanBuffer.State);
        VulkanResourceStates after = VulkanResourceStates.For(state);
        BufferMemoryBarrier2 barrier = new()
        {
            SType = StructureType.BufferMemoryBarrier2,
            SrcStageMask = before.Stages,
            SrcAccessMask = before.Access,
            DstStageMask = after.Stages,
            DstAccessMask = after.Access,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = vulkanBuffer.Handle,
            Offset = 0,
            Size = Vk.WholeSize,
        };
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            BufferMemoryBarrierCount = 1,
            PBufferMemoryBarriers = &barrier,
        };
        _vk.CmdPipelineBarrier2(Handle, &dependency);
        vulkanBuffer.State = state;
    }

    public void CopyBuffer(IBuffer source, long sourceOffset, IBuffer destination, long destinationOffset, long size)
    {
        VulkanBuffer from = (VulkanBuffer)source;
        VulkanBuffer to = (VulkanBuffer)destination;
        BufferCopy region = new((ulong)sourceOffset, (ulong)destinationOffset, (ulong)size);
        _vk.CmdCopyBuffer(Handle, from.Handle, to.Handle, 1, &region);
        MakeVisibleToHost(to);
    }

    public void CopyBufferToTexture(IBuffer source, long sourceOffset, ITexture destination)
    {
        VulkanBuffer from = (VulkanBuffer)source;
        VulkanTexture to = (VulkanTexture)destination;
        BufferImageCopy region = CreateTextureCopy(to, sourceOffset, 0);
        _vk.CmdCopyBufferToImage(Handle, from.Handle, to.Handle, ImageLayout.TransferDstOptimal, 1, &region);
    }

    public void CopyTextureToBuffer(ITexture source, IBuffer destination, long destinationOffset, int mipLevel = 0)
    {
        VulkanTexture from = (VulkanTexture)source;
        VulkanBuffer to = (VulkanBuffer)destination;
        BufferImageCopy region = CreateTextureCopy(from, destinationOffset, mipLevel);
        _vk.CmdCopyImageToBuffer(Handle, from.Handle, ImageLayout.TransferSrcOptimal, to.Handle, 1, &region);
        MakeVisibleToHost(to);
    }

    public void BeginRendering(ReadOnlySpan<ColorTarget> colorTargets, DepthTarget? depthTarget = null)
    {
        RenderingAttachmentInfo* colors = stackalloc RenderingAttachmentInfo[colorTargets.Length];
        Extent2D extent = default;
        for (int index = 0; index < colorTargets.Length; index++)
        {
            ColorTarget target = colorTargets[index];
            VulkanTexture texture = (VulkanTexture)target.Texture;
            ClearValue clear = new(new ClearColorValue(target.ClearColor.X, target.ClearColor.Y, target.ClearColor.Z, target.ClearColor.W));
            colors[index] = CreateAttachment(texture, ImageLayout.ColorAttachmentOptimal, target.Load, clear);
            extent = new Extent2D((uint)texture.Description.Width, (uint)texture.Description.Height);
        }

        RenderingAttachmentInfo depth = default;
        bool hasDepth = depthTarget.HasValue;
        bool hasStencil = false;
        if (depthTarget is { } depthValue)
        {
            VulkanTexture texture = (VulkanTexture)depthValue.Texture;
            ClearValue clear = new(depthStencil: new ClearDepthStencilValue(depthValue.ClearDepth, 0));
            depth = CreateAttachment(texture, ImageLayout.DepthStencilAttachmentOptimal, depthValue.Load, clear);
            hasStencil = texture.Description.Format.HasStencil();
            extent = new Extent2D((uint)texture.Description.Width, (uint)texture.Description.Height);
        }

        RenderingInfo info = new()
        {
            SType = StructureType.RenderingInfo,
            RenderArea = new Rect2D(new Offset2D(0, 0), extent),
            LayerCount = 1,
            ColorAttachmentCount = (uint)colorTargets.Length,
            PColorAttachments = colors,
        };
        if (hasDepth)
        {
            info.PDepthAttachment = &depth;
        }

        if (hasStencil)
        {
            info.PStencilAttachment = &depth;
        }

        _vk.CmdBeginRendering(Handle, &info);
    }

    public void EndRendering()
    {
        _vk.CmdEndRendering(Handle);
    }

    public void SetViewport(in Viewport viewport)
    {
        // A negative height flips y, so clip space is y-up and the image matches Direct3D's (core since Vulkan 1.1).
        Silk.NET.Vulkan.Viewport flipped = new(viewport.X, viewport.Y + viewport.Height, viewport.Width, -viewport.Height, viewport.MinDepth, viewport.MaxDepth);
        _vk.CmdSetViewport(Handle, 0, 1, &flipped);
    }

    public void SetScissor(int x, int y, int width, int height)
    {
        Rect2D scissor = new(new Offset2D(x, y), new Extent2D((uint)width, (uint)height));
        _vk.CmdSetScissor(Handle, 0, 1, &scissor);
    }

    public void SetPipeline(IPipeline pipeline)
    {
        VulkanPipeline vulkanPipeline = (VulkanPipeline)pipeline;
        _vk.CmdBindPipeline(Handle, vulkanPipeline.BindPoint, vulkanPipeline.Handle);
    }

    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, long offset = 0)
    {
        VulkanBuffer vulkanBuffer = (VulkanBuffer)buffer;
        _vk.CmdBindIndexBuffer(Handle, vulkanBuffer.Handle, (ulong)offset, format.ToVulkan());
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
            _vk.CmdPushConstants(Handle, _device.Bindless.PipelineLayout, ShaderStageFlags.All, 0, (uint)size, pointer);
        }
    }

    public void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        _vk.CmdDraw(Handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    public void Dispatch(int groupsX, int groupsY = 1, int groupsZ = 1)
    {
        _vk.CmdDispatch(Handle, (uint)groupsX, (uint)groupsY, (uint)groupsZ);
    }

    public void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
    {
        _vk.CmdDrawIndexed(Handle, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, vertexOffset, (uint)firstInstance);
    }

    /// <summary>
    /// Fills every mip level below the top one by blitting from the level above with linear filtering. Every level must be in
    /// CopyDestination, as after copying the top level in, and they stay there.
    /// </summary>
    internal void FillMipLevels(VulkanTexture texture)
    {
        TextureDescription description = texture.Description;
        for (int level = 1; level < description.MipLevels; level++)
        {
            uint source = (uint)(level - 1);
            MoveMipLevel(texture, source, ImageLayout.TransferDstOptimal, AccessFlags2.TransferWriteBit, ImageLayout.TransferSrcOptimal, AccessFlags2.TransferReadBit);
            TextureDescription from = description.GetMipDescription(level - 1);
            TextureDescription to = description.GetMipDescription(level);
            ImageBlit2 region = new()
            {
                SType = StructureType.ImageBlit2,
                SrcSubresource = new ImageSubresourceLayers(texture.Aspect, source, 0, 1),
                DstSubresource = new ImageSubresourceLayers(texture.Aspect, (uint)level, 0, 1),
            };
            region.SrcOffsets[1] = new Offset3D(from.Width, from.Height, 1);
            region.DstOffsets[1] = new Offset3D(to.Width, to.Height, 1);
            BlitImageInfo2 blit = new()
            {
                SType = StructureType.BlitImageInfo2,
                SrcImage = texture.Handle,
                SrcImageLayout = ImageLayout.TransferSrcOptimal,
                DstImage = texture.Handle,
                DstImageLayout = ImageLayout.TransferDstOptimal,
                RegionCount = 1,
                PRegions = &region,
                Filter = Filter.Linear,
            };
            _vk.CmdBlitImage2(Handle, &blit);
            MoveMipLevel(texture, source, ImageLayout.TransferSrcOptimal, AccessFlags2.TransferReadBit, ImageLayout.TransferDstOptimal, AccessFlags2.TransferWriteBit);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CommandPool pool = _pool;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() => device.Vk.DestroyCommandPool(device.Handle, pool, null));
    }

    private void MoveMipLevel(VulkanTexture texture, uint level, ImageLayout oldLayout, AccessFlags2 oldAccess, ImageLayout newLayout, AccessFlags2 newAccess)
    {
        ImageMemoryBarrier2 barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AllTransferBit,
            SrcAccessMask = oldAccess,
            DstStageMask = PipelineStageFlags2.AllTransferBit,
            DstAccessMask = newAccess,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = texture.Handle,
            SubresourceRange = new ImageSubresourceRange(texture.Aspect, level, 1, 0, 1),
        };
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            ImageMemoryBarrierCount = 1,
            PImageMemoryBarriers = &barrier,
        };
        _vk.CmdPipelineBarrier2(Handle, &dependency);
    }

    private static RenderingAttachmentInfo CreateAttachment(VulkanTexture texture, ImageLayout layout, LoadAction load, ClearValue clear)
    {
        return new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = texture.View,
            ImageLayout = layout,
            LoadOp = load.ToVulkan(),
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = clear,
        };
    }

    private static BufferImageCopy CreateTextureCopy(VulkanTexture texture, long bufferOffset, int mipLevel)
    {
        TextureDescription description = texture.Description.GetMipDescription(mipLevel);
        int pitch = description.Format.GetCopyRowPitch(description.Width);
        int texelsPerRow = pitch / description.Format.GetBytesPerPixel();
        return new BufferImageCopy
        {
            BufferOffset = (ulong)bufferOffset,
            BufferRowLength = (uint)texelsPerRow,
            BufferImageHeight = (uint)description.Height,
            ImageSubresource = new ImageSubresourceLayers(texture.Aspect, (uint)mipLevel, 0, 1),
            ImageExtent = new Extent3D((uint)description.Width, (uint)description.Height, 1),
        };
    }

    /// <summary>Copies into readback memory become visible to the CPU once the submission completes.</summary>
    private void MakeVisibleToHost(VulkanBuffer destination)
    {
        if (destination.Description.Memory != MemoryLocation.Readback)
        {
            return;
        }

        MemoryBarrier2 barrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AllTransferBit,
            SrcAccessMask = AccessFlags2.TransferWriteBit,
            DstStageMask = PipelineStageFlags2.HostBit,
            DstAccessMask = AccessFlags2.HostReadBit,
        };
        DependencyInfo dependency = new()
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = &barrier,
        };
        _vk.CmdPipelineBarrier2(Handle, &dependency);
    }
}

