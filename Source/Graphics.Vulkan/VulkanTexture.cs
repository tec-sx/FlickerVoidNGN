using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>A 2D Vulkan image with its own memory and one view of the whole image.</summary>
internal sealed unsafe class VulkanTexture : ITexture
{
    private readonly VulkanGraphicsDevice _device;
    private readonly DeviceMemory _memory;
    private bool _disposed;

    public VulkanTexture(VulkanGraphicsDevice device, in TextureDescription description, ImageUsageFlags extraUsage)
    {
        _device = device;
        Description = description;
        Vk vk = device.Vk;
        Format format = description.Format.ToVulkan();

        ImageCreateInfo info = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D((uint)description.Width, (uint)description.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = GetUsage(description.Usage) | extraUsage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };
        VulkanResult.Check(vk.CreateImage(device.Handle, &info, null, out Image image));
        Handle = image;

        vk.GetImageMemoryRequirements(device.Handle, image, out MemoryRequirements requirements);
        _memory = device.AllocateMemory(requirements, MemoryPropertyFlags.DeviceLocalBit);
        VulkanResult.Check(vk.BindImageMemory(device.Handle, image, _memory, 0));

        Aspect = description.Format.GetAspect();
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(Aspect, 0, 1, 0, 1),
        };
        VulkanResult.Check(vk.CreateImageView(device.Handle, &viewInfo, null, out ImageView view));
        View = view;

        if (description.Usage.HasFlag(TextureUsage.ShaderRead))
        {
            BindlessIndex = device.Bindless.Resources.Allocate();
            device.Bindless.WriteTexture(BindlessIndex, view);
        }

        device.SetName(ObjectType.Image, image.Handle, description.Name);
    }

    public Image Handle { get; }

    public ImageView View { get; }

    public ImageAspectFlags Aspect { get; }

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
        Image image = Handle;
        ImageView view = View;
        DeviceMemory memory = _memory;
        uint index = BindlessIndex;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroyImageView(device.Handle, view, null);
            device.Vk.DestroyImage(device.Handle, image, null);
            device.Vk.FreeMemory(device.Handle, memory, null);
            device.Bindless.Resources.Free(index);
        });
    }

    private static ImageUsageFlags GetUsage(TextureUsage usage)
    {
        ImageUsageFlags flags = 0;
        if (usage.HasFlag(TextureUsage.ShaderRead))
        {
            flags |= ImageUsageFlags.SampledBit;
        }

        if (usage.HasFlag(TextureUsage.RenderTarget))
        {
            flags |= ImageUsageFlags.ColorAttachmentBit;
        }

        if (usage.HasFlag(TextureUsage.DepthStencil))
        {
            flags |= ImageUsageFlags.DepthStencilAttachmentBit;
        }

        if (usage.HasFlag(TextureUsage.CopySource))
        {
            flags |= ImageUsageFlags.TransferSrcBit;
        }

        if (usage.HasFlag(TextureUsage.CopyDestination))
        {
            flags |= ImageUsageFlags.TransferDstBit;
        }

        return flags;
    }
}
