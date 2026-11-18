using Silk.NET.Vulkan;
using VkSamplerAddressMode = Silk.NET.Vulkan.SamplerAddressMode;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>A Vulkan sampler in the bindless sampler table.</summary>
internal sealed unsafe class VulkanSampler : ISampler
{
    private readonly VulkanGraphicsDevice _device;
    private bool _disposed;

    public VulkanSampler(VulkanGraphicsDevice device, in SamplerDescription description)
    {
        _device = device;
        Description = description;
        Filter filter = description.Filter.ToVulkan();
        VkSamplerAddressMode address = description.AddressMode.ToVulkan();

        SamplerCreateInfo info = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = filter,
            MinFilter = filter,
            MipmapMode = description.Filter.ToVulkanMipmap(),
            AddressModeU = address,
            AddressModeV = address,
            AddressModeW = address,
            MaxLod = Vk.LodClampNone,
        };
        VulkanResult.Check(device.Vk.CreateSampler(device.Handle, &info, null, out Sampler sampler));
        Handle = sampler;

        BindlessIndex = device.Bindless.Samplers.Allocate();
        device.Bindless.WriteSampler(BindlessIndex, sampler);
    }

    public Sampler Handle { get; }

    public SamplerDescription Description { get; }

    public uint BindlessIndex { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Sampler sampler = Handle;
        uint index = BindlessIndex;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroySampler(device.Handle, sampler, null);
            device.Bindless.Samplers.Free(index);
        });
    }
}
