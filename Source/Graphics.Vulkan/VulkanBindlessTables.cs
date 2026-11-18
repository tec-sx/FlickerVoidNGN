using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// The device-wide descriptor sets shaders index into, and the one pipeline layout every pipeline uses.
/// DXC maps register space N to set N, so set 1 holds Texture2D[] (t0, space1), set 2 ByteAddressBuffer[]
/// (t0, space2) and set 3 SamplerState[] (s0, space3); set 0 is empty. Push constants are b0, space0.
/// </summary>
internal sealed unsafe class VulkanBindlessTables : IDisposable
{
    public const uint TextureSet = 1;
    public const uint BufferSet = 2;
    public const uint SamplerSet = 3;
    private const int SetCount = 4;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly DescriptorSetLayout[] _layouts = new DescriptorSetLayout[SetCount];
    private readonly DescriptorSet[] _sets = new DescriptorSet[SetCount];
    private readonly DescriptorPool _pool;

    public VulkanBindlessTables(Vk vk, Device device, in PhysicalDeviceVulkan12Properties limits)
    {
        _vk = vk;
        _device = device;
        ResourceCapacity = (uint)Math.Min(GraphicsLimits.BindlessResourceCapacity, Math.Min(limits.MaxDescriptorSetUpdateAfterBindSampledImages, limits.MaxDescriptorSetUpdateAfterBindStorageBuffers));
        SamplerCapacity = (uint)Math.Min(GraphicsLimits.BindlessSamplerCapacity, limits.MaxDescriptorSetUpdateAfterBindSamplers);
        Resources = new BindlessIndexAllocator((int)ResourceCapacity);
        Samplers = new BindlessIndexAllocator((int)SamplerCapacity);

        _layouts[0] = CreateEmptyLayout();
        _layouts[TextureSet] = CreateArrayLayout(DescriptorType.SampledImage, ResourceCapacity);
        _layouts[BufferSet] = CreateArrayLayout(DescriptorType.StorageBuffer, ResourceCapacity);
        _layouts[SamplerSet] = CreateArrayLayout(DescriptorType.Sampler, SamplerCapacity);
        _pool = CreatePool();
        AllocateSets();
        PipelineLayout = CreatePipelineLayout();
    }

    public uint ResourceCapacity { get; }

    public uint SamplerCapacity { get; }

    /// <summary>Slots shared by textures and buffers.</summary>
    public BindlessIndexAllocator Resources { get; }

    public BindlessIndexAllocator Samplers { get; }

    public PipelineLayout PipelineLayout { get; }

    public void Bind(CommandBuffer commandBuffer, PipelineBindPoint bindPoint)
    {
        fixed (DescriptorSet* sets = _sets)
        {
            _vk.CmdBindDescriptorSets(commandBuffer, bindPoint, PipelineLayout, 0, SetCount, sets, 0, null);
        }
    }

    public void WriteTexture(uint index, ImageView view)
    {
        DescriptorImageInfo image = new() { ImageView = view, ImageLayout = ImageLayout.ShaderReadOnlyOptimal };
        WriteDescriptorSet write = CreateWrite(TextureSet, index, DescriptorType.SampledImage);
        write.PImageInfo = &image;
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    public void WriteBuffer(uint index, Silk.NET.Vulkan.Buffer buffer)
    {
        DescriptorBufferInfo info = new() { Buffer = buffer, Offset = 0, Range = Vk.WholeSize };
        WriteDescriptorSet write = CreateWrite(BufferSet, index, DescriptorType.StorageBuffer);
        write.PBufferInfo = &info;
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    public void WriteSampler(uint index, Sampler sampler)
    {
        DescriptorImageInfo image = new() { Sampler = sampler };
        WriteDescriptorSet write = CreateWrite(SamplerSet, index, DescriptorType.Sampler);
        write.PImageInfo = &image;
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    public void Dispose()
    {
        _vk.DestroyPipelineLayout(_device, PipelineLayout, null);
        _vk.DestroyDescriptorPool(_device, _pool, null);
        foreach (DescriptorSetLayout layout in _layouts)
        {
            _vk.DestroyDescriptorSetLayout(_device, layout, null);
        }
    }

    private WriteDescriptorSet CreateWrite(uint set, uint index, DescriptorType type)
    {
        return new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _sets[set],
            DstBinding = 0,
            DstArrayElement = index,
            DescriptorCount = 1,
            DescriptorType = type,
        };
    }

    private DescriptorSetLayout CreateEmptyLayout()
    {
        DescriptorSetLayoutCreateInfo info = new() { SType = StructureType.DescriptorSetLayoutCreateInfo };
        VulkanResult.Check(_vk.CreateDescriptorSetLayout(_device, &info, null, out DescriptorSetLayout layout));
        return layout;
    }

    private DescriptorSetLayout CreateArrayLayout(DescriptorType type, uint count)
    {
        DescriptorSetLayoutBinding binding = new()
        {
            Binding = 0,
            DescriptorType = type,
            DescriptorCount = count,
            StageFlags = ShaderStageFlags.All,
        };

        // Partially bound: only the slots a shader reads must hold a descriptor. Update after bind: slots change while command lists are in flight.
        DescriptorBindingFlags flags = DescriptorBindingFlags.PartiallyBoundBit | DescriptorBindingFlags.UpdateAfterBindBit | DescriptorBindingFlags.UpdateUnusedWhilePendingBit;
        DescriptorSetLayoutBindingFlagsCreateInfo bindingFlags = new()
        {
            SType = StructureType.DescriptorSetLayoutBindingFlagsCreateInfo,
            BindingCount = 1,
            PBindingFlags = &flags,
        };
        DescriptorSetLayoutCreateInfo info = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            PNext = &bindingFlags,
            Flags = DescriptorSetLayoutCreateFlags.UpdateAfterBindPoolBit,
            BindingCount = 1,
            PBindings = &binding,
        };
        VulkanResult.Check(_vk.CreateDescriptorSetLayout(_device, &info, null, out DescriptorSetLayout layout));
        return layout;
    }

    private DescriptorPool CreatePool()
    {
        DescriptorPoolSize* sizes = stackalloc DescriptorPoolSize[3];
        sizes[0] = new DescriptorPoolSize(DescriptorType.SampledImage, ResourceCapacity);
        sizes[1] = new DescriptorPoolSize(DescriptorType.StorageBuffer, ResourceCapacity);
        sizes[2] = new DescriptorPoolSize(DescriptorType.Sampler, SamplerCapacity);
        DescriptorPoolCreateInfo info = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.UpdateAfterBindBit,
            MaxSets = SetCount,
            PoolSizeCount = 3,
            PPoolSizes = sizes,
        };
        VulkanResult.Check(_vk.CreateDescriptorPool(_device, &info, null, out DescriptorPool pool));
        return pool;
    }

    private void AllocateSets()
    {
        fixed (DescriptorSetLayout* layouts = _layouts)
        fixed (DescriptorSet* sets = _sets)
        {
            DescriptorSetAllocateInfo info = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _pool,
                DescriptorSetCount = SetCount,
                PSetLayouts = layouts,
            };
            VulkanResult.Check(_vk.AllocateDescriptorSets(_device, &info, sets));
        }
    }

    private PipelineLayout CreatePipelineLayout()
    {
        PushConstantRange pushConstants = new(ShaderStageFlags.All, 0, GraphicsLimits.PushConstantSize);
        fixed (DescriptorSetLayout* layouts = _layouts)
        {
            PipelineLayoutCreateInfo info = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = SetCount,
                PSetLayouts = layouts,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstants,
            };
            VulkanResult.Check(_vk.CreatePipelineLayout(_device, &info, null, out PipelineLayout layout));
            return layout;
        }
    }
}
