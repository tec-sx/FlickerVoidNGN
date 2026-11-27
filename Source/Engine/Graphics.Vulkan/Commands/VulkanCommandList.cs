using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Vulkan.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Vulkan.Buffers;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Pipelines;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;

using Viewport = FlickerVoid.Engine.Graphics.Commands.Viewport;

namespace FlickerVoid.Engine.Graphics.Vulkan.Commands;

/// <summary>
/// A command buffer with its own pool, reset on Begin once the GPU has finished its previous submission, the scratch memory its
/// acceleration structure builds share, kept until the builds recorded with it have run, and a timestamp query pool for its
/// timers, read back on Begin.
/// </summary>
internal sealed unsafe class VulkanCommandList : ICommandList
{
    /// <summary>The width and height in texels of a block of a block-compressed format.</summary>
    private const int BlockTexels = 4;

    private readonly VulkanGraphicsDevice _device;
    private readonly Vk _vk;
    private readonly CommandPool _pool;
    // Scratch that builds recorded since Begin use and a larger buffer replaced; released once they have run.
    private readonly List<VulkanBuffer> _replacedScratch = [];
    private VulkanBuffer? _scratch;
    private ulong _scratchAddress;
    // Timers recorded since Begin, a start and an end query each, and those still open, -1 for one past the capacity.
    private readonly List<TimerRecord> _timers = [];
    private readonly Stack<int> _openTimers = new();
    private readonly List<GpuTiming> _timings = [];
    private readonly ulong[] _timestamps = new ulong[GraphicsLimits.TimersPerCommandList * QueriesPerTimer];
    private readonly QueryPool _queries;
    private readonly bool _timed;
    private ulong _readSubmission;
    private bool _disposed;

    public VulkanCommandList(VulkanGraphicsDevice device)
    {
        _device = device;
        _vk = device.Vk;
        _timed = device.Features.HasFlag(GraphicsFeatures.Timestamps);
        if (_timed)
        {
            QueryPoolCreateInfo queryInfo = new()
            {
                SType = StructureType.QueryPoolCreateInfo,
                QueryType = QueryType.Timestamp,
                QueryCount = (uint)_timestamps.Length,
            };
            VulkanResult.Check(_vk.CreateQueryPool(device.Handle, &queryInfo, null, out QueryPool queries));
            _queries = queries;
        }

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

    /// <summary>Queries per timer: its start and its end.</summary>
    private const int QueriesPerTimer = 2;

    public CommandBuffer Handle { get; }

    /// <summary>The device timeline value its last submission signals; 0 before the first.</summary>
    public ulong SubmissionValue { get; set; }

    public IReadOnlyList<GpuTiming> Timings => _timings;

    public void Begin()
    {
        _device.WaitFor(SubmissionValue);
        ReadTimings();
        ReleaseReplacedScratch();
        VulkanResult.Check(_vk.ResetCommandPool(_device.Handle, _pool, 0));
        CommandBufferBeginInfo info = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        VulkanResult.Check(_vk.BeginCommandBuffer(Handle, &info));
        _device.Bindless.Bind(Handle, PipelineBindPoint.Graphics);
        _device.Bindless.Bind(Handle, PipelineBindPoint.Compute);
        if (_timed)
        {
            _vk.CmdResetQueryPool(Handle, _queries, 0, (uint)_timestamps.Length);
        }
    }

    public void End()
    {
        if (_openTimers.Count > 0)
        {
            throw new InvalidOperationException("A timer is still open; end every timer before ending the command list.");
        }

        VulkanResult.Check(_vk.EndCommandBuffer(Handle));
    }

    public void BeginTimer(string name)
    {
        int index = _timers.Count;
        if (index >= GraphicsLimits.TimersPerCommandList)
        {
            _openTimers.Push(-1);
            return;
        }

        _timers.Add(new TimerRecord(name, _openTimers.Count));
        _openTimers.Push(index);
        WriteTimestamp(index * QueriesPerTimer);
    }

    public void EndTimer()
    {
        if (!_openTimers.TryPop(out int index))
        {
            throw new InvalidOperationException("No timer is open.");
        }

        if (index >= 0)
        {
            WriteTimestamp((index * QueriesPerTimer) + 1);
        }
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

    public void CopyBufferToTexture(IBuffer source, long sourceOffset, ITexture destination, int mipLevel = 0)
    {
        VulkanBuffer from = (VulkanBuffer)source;
        VulkanTexture to = (VulkanTexture)destination;
        BufferImageCopy region = CreateTextureCopy(to, sourceOffset, mipLevel);
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

    public void CopyTexture(ITexture source, ITexture destination, int destinationX = 0, int destinationY = 0)
    {
        VulkanTexture from = (VulkanTexture)source;
        VulkanTexture to = (VulkanTexture)destination;
        ImageSubresourceLayers fromLevel = new(from.Aspect, 0, 0, 1);
        ImageSubresourceLayers toLevel = new(to.Aspect, 0, 0, 1);
        Extent3D extent = new((uint)from.Description.Width, (uint)from.Description.Height, 1);
        Offset3D offset = new(destinationX, destinationY, 0);
        ImageCopy region = new() { SrcSubresource = fromLevel, DstSubresource = toLevel, DstOffset = offset, Extent = extent };
        _vk.CmdCopyImage(Handle, from.Handle, ImageLayout.TransferSrcOptimal, to.Handle, ImageLayout.TransferDstOptimal, 1, &region);
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

    public void DrawIndexedIndirect(IBuffer arguments, long offset = 0)
    {
        VulkanBuffer buffer = (VulkanBuffer)arguments;
        _vk.CmdDrawIndexedIndirect(Handle, buffer.Handle, (ulong)offset, 1, 20);
    }

    public void BuildBottomLevels(ReadOnlySpan<BottomLevelBuild> builds)
    {
        if (builds.IsEmpty)
        {
            return;
        }

        KhrAccelerationStructure extension = _device.RequireAccelerationStructures();
        int count = builds.Length;
        VulkanAccelerationStructure[] structures = new VulkanAccelerationStructure[count];
        ulong[] scratchOffsets = new ulong[count];
        ulong scratchSize = 0;
        for (int index = 0; index < count; index++)
        {
            BottomLevelBuild build = builds[index];
            VulkanAccelerationStructure structure = CheckBottomLevel(build);
            structures[index] = structure;
            ulong needed = structure.BuildScratchSize;
            if (build.Refit)
            {
                needed = structure.UpdateScratchSize;
            }

            scratchOffsets[index] = AlignScratch(scratchSize);
            scratchSize = scratchOffsets[index] + needed;
        }

        ulong scratch = EnsureScratch(scratchSize);
        AccelerationStructureGeometryKHR[] geometries = new AccelerationStructureGeometryKHR[count];
        AccelerationStructureBuildGeometryInfoKHR[] infos = new AccelerationStructureBuildGeometryInfoKHR[count];
        AccelerationStructureBuildRangeInfoKHR[] ranges = new AccelerationStructureBuildRangeInfoKHR[count];
        AccelerationStructureBuildRangeInfoKHR*[] rangePointers = new AccelerationStructureBuildRangeInfoKHR*[count];
        fixed (AccelerationStructureGeometryKHR* geometryPointer = geometries)
        fixed (AccelerationStructureBuildGeometryInfoKHR* infoPointer = infos)
        fixed (AccelerationStructureBuildRangeInfoKHR* rangePointer = ranges)
        fixed (AccelerationStructureBuildRangeInfoKHR** rangePointerPointer = rangePointers)
        {
            for (int index = 0; index < count; index++)
            {
                BottomLevelBuild build = builds[index];
                VulkanAccelerationStructure structure = structures[index];
                BottomLevelGeometry geometry = build.Geometry;
                VulkanBuffer vertices = (VulkanBuffer)geometry.Vertices;
                VulkanBuffer indices = (VulkanBuffer)geometry.Indices;
                geometries[index] = VulkanAccelerationStructure.DescribeTriangles(vertices.DeviceAddress, geometry.VertexCount, geometry.VertexStride, indices.DeviceAddress);
                infos[index] = new AccelerationStructureBuildGeometryInfoKHR
                {
                    SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
                    Type = AccelerationStructureTypeKHR.BottomLevelKhr,
                    Flags = VulkanAccelerationStructure.GetBottomLevelFlags(structure.Refittable),
                    Mode = GetBuildMode(build.Refit),
                    SrcAccelerationStructure = build.Refit ? structure.Handle : default,
                    DstAccelerationStructure = structure.Handle,
                    GeometryCount = 1,
                    PGeometries = geometryPointer + index,
                    ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratch + scratchOffsets[index] },
                };
                ranges[index] = new AccelerationStructureBuildRangeInfoKHR { PrimitiveCount = (uint)(geometry.IndexCount / 3) };
                rangePointers[index] = rangePointer + index;
            }

            WaitBeforeBuilds();
            extension.CmdBuildAccelerationStructures(Handle, (uint)count, infoPointer, rangePointerPointer);
            MakeBuildsReadable();
        }

        foreach (VulkanAccelerationStructure structure in structures)
        {
            structure.Built = true;
        }
    }

    public void BuildTopLevel(IAccelerationStructure structure, IBuffer instances, int instanceCount)
    {
        KhrAccelerationStructure extension = _device.RequireAccelerationStructures();
        VulkanAccelerationStructure topLevel = (VulkanAccelerationStructure)structure;
        VulkanBuffer instanceBuffer = (VulkanBuffer)instances;
        if (topLevel.Type != AccelerationStructureTypeKHR.TopLevelKhr)
        {
            throw new ArgumentException("Only a top-level structure is built from instances.", nameof(structure));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(instanceCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instanceCount, topLevel.InstanceCapacity);
        if (instanceBuffer.DeviceAddress == 0)
        {
            throw new ArgumentException($"The instances' buffer needs {nameof(BufferUsage.AccelerationStructureInput)}.", nameof(instances));
        }

        ulong scratch = EnsureScratch(topLevel.BuildScratchSize);
        AccelerationStructureGeometryKHR geometry = VulkanAccelerationStructure.DescribeInstances(instanceBuffer.DeviceAddress);
        AccelerationStructureBuildGeometryInfoKHR info = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.TopLevelKhr,
            Flags = VulkanAccelerationStructure.TopLevelFlags,
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            DstAccelerationStructure = topLevel.Handle,
            GeometryCount = 1,
            PGeometries = &geometry,
            ScratchData = new DeviceOrHostAddressKHR { DeviceAddress = scratch },
        };
        AccelerationStructureBuildRangeInfoKHR range = new() { PrimitiveCount = (uint)instanceCount };
        AccelerationStructureBuildRangeInfoKHR* rangePointer = &range;
        WaitBeforeBuilds();
        extension.CmdBuildAccelerationStructures(Handle, 1, &info, &rangePointer);
        MakeBuildsReadable();
        topLevel.Built = true;
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
        ReleaseReplacedScratch();
        _scratch?.Dispose();
        CommandPool pool = _pool;
        QueryPool queries = _queries;
        bool timed = _timed;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroyCommandPool(device.Handle, pool, null);
            if (timed)
            {
                device.Vk.DestroyQueryPool(device.Handle, queries, null);
            }
        });
    }

    /// <summary>Writes the query once the GPU has finished everything recorded before it.</summary>
    private void WriteTimestamp(int query)
    {
        if (_timed)
        {
            _vk.CmdWriteTimestamp2(Handle, PipelineStageFlags2.AllCommandsBit, _queries, (uint)query);
        }
    }

    /// <summary>
    /// Turns the timestamps of the last recording into timings when that recording was submitted, which Begin has waited for, and
    /// forgets its timers.
    /// </summary>
    private void ReadTimings()
    {
        _timings.Clear();
        bool submitted = SubmissionValue != _readSubmission;
        _readSubmission = SubmissionValue;
        int count = _timers.Count;
        if (_timed && submitted && count > 0)
        {
            int queryCount = count * QueriesPerTimer;
            Result result;
            fixed (ulong* data = _timestamps)
            {
                nuint size = (nuint)(queryCount * sizeof(ulong));
                result = _vk.GetQueryPoolResults(_device.Handle, _queries, 0, (uint)queryCount, size, data, sizeof(ulong), QueryResultFlags.Result64Bit);
            }

            if (result == Result.Success)
            {
                AddTimings(count);
            }
        }

        _timers.Clear();
        _openTimers.Clear();
    }

    private void AddTimings(int count)
    {
        ulong mask = _device.TimestampMask;
        double millisecondsPerTick = _device.TimestampPeriod / 1_000_000.0;
        ulong first = _timestamps[0] & mask;
        for (int index = 0; index < count; index++)
        {
            ulong start = _timestamps[index * QueriesPerTimer] & mask;
            ulong end = _timestamps[(index * QueriesPerTimer) + 1] & mask;
            double startMilliseconds = (start - first) * millisecondsPerTick;
            double milliseconds = (end - start) * millisecondsPerTick;
            TimerRecord timer = _timers[index];
            _timings.Add(new GpuTiming(timer.Name, timer.Depth, startMilliseconds, milliseconds));
        }
    }

    private static VulkanAccelerationStructure CheckBottomLevel(in BottomLevelBuild build)
    {
        VulkanAccelerationStructure structure = (VulkanAccelerationStructure)build.Structure;
        BottomLevelGeometry geometry = build.Geometry;
        if (structure.Type != AccelerationStructureTypeKHR.BottomLevelKhr)
        {
            throw new ArgumentException("Only a bottom-level structure is built from triangles.", nameof(build));
        }

        if (geometry.VertexCount > structure.VertexCount || geometry.IndexCount > structure.IndexCount || geometry.IndexCount % 3 != 0)
        {
            throw new ArgumentException($"The structure holds up to {structure.VertexCount} vertices and {structure.IndexCount} indices of whole triangles; got {geometry.VertexCount} and {geometry.IndexCount}.", nameof(build));
        }

        if (((VulkanBuffer)geometry.Vertices).DeviceAddress == 0 || ((VulkanBuffer)geometry.Indices).DeviceAddress == 0)
        {
            throw new ArgumentException($"The vertices' and indices' buffers need {nameof(BufferUsage.AccelerationStructureInput)}.", nameof(build));
        }

        if (build.Refit && (!structure.Refittable || !structure.Built))
        {
            throw new InvalidOperationException("Only a refittable structure that was built can be refitted.");
        }

        return structure;
    }

    private static BuildAccelerationStructureModeKHR GetBuildMode(bool refit)
    {
        if (refit)
        {
            return BuildAccelerationStructureModeKHR.UpdateKhr;
        }

        return BuildAccelerationStructureModeKHR.BuildKhr;
    }

    private ulong AlignScratch(ulong offset)
    {
        ulong alignment = _device.ScratchAlignment;
        return (offset + alignment - 1) / alignment * alignment;
    }

    /// <summary>
    /// The aligned address of scratch memory of at least the size; a larger buffer replaces a smaller one, which builds recorded
    /// before may still use, so it is released only once they have run. Builds in one list share it: each waits for the one before.
    /// </summary>
    private ulong EnsureScratch(ulong size)
    {
        ulong alignment = _device.ScratchAlignment;
        ulong needed = size + alignment;
        if (_scratch is null || (ulong)_scratch.Description.Size < needed)
        {
            if (_scratch is not null)
            {
                _replacedScratch.Add(_scratch);
            }

            ulong capacity = BitOperations.RoundUpToPowerOf2(Math.Max(needed, 1));
            BufferDescription description = new((long)capacity, BufferUsage.None) { Name = "Acceleration structure scratch" };
            _scratch = new VulkanBuffer(_device, description, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.ShaderDeviceAddressBit);
            _scratchAddress = AlignScratch(_scratch.DeviceAddress);
        }

        return _scratchAddress;
    }

    /// <summary>Releases the scratch larger buffers replaced, once the GPU is done with the list that recorded builds in it.</summary>
    private void ReleaseReplacedScratch()
    {
        foreach (VulkanBuffer scratch in _replacedScratch)
        {
            scratch.Dispose();
        }

        _replacedScratch.Clear();
    }

    /// <summary>Builds read what every earlier command wrote, and write only after earlier reads and builds are done.</summary>
    private void WaitBeforeBuilds()
    {
        MemoryBarrier2 barrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AllCommandsBit,
            SrcAccessMask = AccessFlags2.MemoryWriteBit,
            DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
            DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr | AccessFlags2.AccelerationStructureWriteBitKhr | AccessFlags2.ShaderReadBit,
        };
        PipelineBarrier(barrier);
    }

    /// <summary>Later builds and shaders' ray queries read what the builds wrote.</summary>
    private void MakeBuildsReadable()
    {
        MemoryBarrier2 barrier = new()
        {
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr,
            SrcAccessMask = AccessFlags2.AccelerationStructureWriteBitKhr,
            DstStageMask = PipelineStageFlags2.AccelerationStructureBuildBitKhr | PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit,
            DstAccessMask = AccessFlags2.AccelerationStructureReadBitKhr,
        };
        PipelineBarrier(barrier);
    }

    private void PipelineBarrier(in MemoryBarrier2 barrier)
    {
        fixed (MemoryBarrier2* pointer = &barrier)
        {
            DependencyInfo dependency = new()
            {
                SType = StructureType.DependencyInfo,
                MemoryBarrierCount = 1,
                PMemoryBarriers = pointer,
            };
            _vk.CmdPipelineBarrier2(Handle, &dependency);
        }
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
        // Row length and image height count texels, whole blocks of them for block-compressed formats.
        TextureDescription description = texture.Description.GetMipDescription(mipLevel);
        PixelFormat format = description.Format;
        int pitch = format.GetCopyRowPitch(description.Width);
        int texelsPerRow;
        int rows;
        if (format.IsBlockCompressed())
        {
            int bytesPerBlock = format.GetRowSize(BlockTexels);
            texelsPerRow = pitch / bytesPerBlock * BlockTexels;
            rows = format.GetRowCount(description.Height) * BlockTexels;
        }
        else
        {
            texelsPerRow = pitch / format.GetBytesPerPixel();
            rows = description.Height;
        }

        return new BufferImageCopy
        {
            BufferOffset = (ulong)bufferOffset,
            BufferRowLength = (uint)texelsPerRow,
            BufferImageHeight = (uint)rows,
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

    /// <summary>A timer recorded since Begin: its queries are at twice its index and the one after.</summary>
    private readonly record struct TimerRecord(string Name, int Depth);
}
