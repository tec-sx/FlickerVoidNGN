using Silk.NET.Vulkan;
using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>A Vulkan buffer with its own memory; upload and readback buffers stay mapped.</summary>
internal sealed unsafe class VulkanBuffer : IBuffer
{
    private readonly VulkanGraphicsDevice _device;
    private readonly DeviceMemory _memory;
    private readonly byte* _mapped;
    private bool _disposed;

    public VulkanBuffer(VulkanGraphicsDevice device, in BufferDescription description, BufferUsageFlags extraUsage)
    {
        _device = device;
        Description = description;
        Vk vk = device.Vk;

        BufferCreateInfo info = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = (ulong)description.Size,
            Usage = GetUsage(description.Usage) | extraUsage,
            SharingMode = SharingMode.Exclusive,
        };
        VulkanResult.Check(vk.CreateBuffer(device.Handle, &info, null, out VkBuffer buffer));
        Handle = buffer;

        vk.GetBufferMemoryRequirements(device.Handle, buffer, out MemoryRequirements requirements);
        MemoryPropertyFlags properties = GetMemoryProperties(description.Memory);
        _memory = device.AllocateMemory(requirements, properties);
        VulkanResult.Check(vk.BindBufferMemory(device.Handle, buffer, _memory, 0));

        if (description.Memory != MemoryLocation.Gpu)
        {
            void* mapped;
            VulkanResult.Check(vk.MapMemory(device.Handle, _memory, 0, Vk.WholeSize, 0, &mapped));
            _mapped = (byte*)mapped;
        }

        if (description.Usage.HasFlag(BufferUsage.ShaderRead) || description.Usage.HasFlag(BufferUsage.ShaderWrite))
        {
            BindlessIndex = device.Bindless.Resources.Allocate();
            device.Bindless.WriteBuffer(BindlessIndex, buffer);
        }

        device.SetName(ObjectType.Buffer, buffer.Handle, description.Name);
    }

    public VkBuffer Handle { get; }

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
        VkBuffer handle = Handle;
        DeviceMemory memory = _memory;
        uint index = BindlessIndex;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroyBuffer(device.Handle, handle, null);
            device.Vk.FreeMemory(device.Handle, memory, null);
            device.Bindless.Resources.Free(index);
        });
    }

    private static BufferUsageFlags GetUsage(BufferUsage usage)
    {
        BufferUsageFlags flags = 0;
        if (usage.HasFlag(BufferUsage.ShaderRead) || usage.HasFlag(BufferUsage.ShaderWrite))
        {
            flags |= BufferUsageFlags.StorageBufferBit;
        }

        if (usage.HasFlag(BufferUsage.Index))
        {
            flags |= BufferUsageFlags.IndexBufferBit;
        }

        if (usage.HasFlag(BufferUsage.CopySource))
        {
            flags |= BufferUsageFlags.TransferSrcBit;
        }

        if (usage.HasFlag(BufferUsage.CopyDestination))
        {
            flags |= BufferUsageFlags.TransferDstBit;
        }

        return flags;
    }

    private static MemoryPropertyFlags GetMemoryProperties(MemoryLocation location)
    {
        switch (location)
        {
            case MemoryLocation.Gpu:
                return MemoryPropertyFlags.DeviceLocalBit;
            case MemoryLocation.Upload:
                return MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
            case MemoryLocation.Readback:
                return MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit;
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
