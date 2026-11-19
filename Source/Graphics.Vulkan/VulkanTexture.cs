using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>
/// A 2D Vulkan image, with its own memory unless something else owns it, one view of every mip level, and a view of the top level
/// shaders write to when it has <see cref="TextureUsage.ShaderWrite"/>. Memory created for export
/// is a dedicated allocation, which is what importers such as Avalonia allocate on their side.
/// </summary>
internal sealed unsafe class VulkanTexture : ITexture
{
    private readonly VulkanGraphicsDevice _device;
    private readonly bool _ownsImage;
    private bool _disposed;

    public VulkanTexture(VulkanGraphicsDevice device, in TextureDescription description, ImageUsageFlags extraUsage, ExternalMemoryHandleTypeFlags exportTypes = 0)
    {
        _device = device;
        Description = description;
        Vk vk = device.Vk;

        ExternalMemoryImageCreateInfo external = new()
        {
            SType = StructureType.ExternalMemoryImageCreateInfo,
            HandleTypes = exportTypes,
        };
        ImageCreateInfo info = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = description.Format.ToVulkan(),
            Extent = new Extent3D((uint)description.Width, (uint)description.Height, 1),
            MipLevels = (uint)description.MipLevels,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = GetUsage(description.Usage) | extraUsage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };
        if (exportTypes != 0)
        {
            info.PNext = &external;
        }

        VulkanResult.Check(vk.CreateImage(device.Handle, &info, null, out Image image));
        Handle = image;
        _ownsImage = true;

        vk.GetImageMemoryRequirements(device.Handle, image, out MemoryRequirements requirements);
        MemoryDedicatedAllocateInfo dedicated = new()
        {
            SType = StructureType.MemoryDedicatedAllocateInfo,
            Image = image,
        };
        ExportMemoryAllocateInfo export = new()
        {
            SType = StructureType.ExportMemoryAllocateInfo,
            PNext = &dedicated,
            HandleTypes = exportTypes,
        };
        void* next = null;
        if (exportTypes != 0)
        {
            next = &export;
        }

        Memory = device.AllocateMemory(requirements, MemoryPropertyFlags.DeviceLocalBit, next);
        MemorySize = (long)requirements.Size;
        VulkanResult.Check(vk.BindImageMemory(device.Handle, image, Memory, 0));

        Aspect = description.Format.GetAspect();
        View = CreateView(description.MipLevels);
        bool read = description.Usage.HasFlag(TextureUsage.ShaderRead);
        bool write = description.Usage.HasFlag(TextureUsage.ShaderWrite);
        if (read || write)
        {
            BindlessIndex = device.Bindless.Resources.Allocate();
        }

        if (read)
        {
            device.Bindless.WriteTexture(BindlessIndex, View);
        }

        if (write)
        {
            StorageView = CreateView(1);
            device.Bindless.WriteStorageImage(BindlessIndex, StorageView);
        }

        device.SetName(ObjectType.Image, image.Handle, description.Name);
    }

    /// <summary>Wraps an image something else owns, such as a swapchain back buffer; disposing it destroys only its view.</summary>
    public VulkanTexture(VulkanGraphicsDevice device, in TextureDescription description, Image image)
    {
        _device = device;
        Description = description;
        Handle = image;
        Aspect = description.Format.GetAspect();
        View = CreateView(1);
        device.SetName(ObjectType.Image, image.Handle, description.Name);
    }

    public Image Handle { get; }

    /// <summary>The memory the image is bound to, when it owns its image.</summary>
    public DeviceMemory Memory { get; }

    public long MemorySize { get; }

    public ImageView View { get; }

    /// <summary>The top level, for shader writes; default when the texture has no <see cref="TextureUsage.ShaderWrite"/>.</summary>
    public ImageView StorageView { get; }

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
        ImageView storageView = StorageView;
        DeviceMemory memory = Memory;
        bool ownsImage = _ownsImage;
        uint index = BindlessIndex;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.Vk.DestroyImageView(device.Handle, view, null);
            if (storageView.Handle != 0)
            {
                device.Vk.DestroyImageView(device.Handle, storageView, null);
            }
            if (ownsImage)
            {
                device.Vk.DestroyImage(device.Handle, image, null);
                device.Vk.FreeMemory(device.Handle, memory, null);
            }

            device.Bindless.Resources.Free(index);
        });
    }

    private ImageView CreateView(int mipLevels)
    {
        ImageViewCreateInfo info = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = Handle,
            ViewType = ImageViewType.Type2D,
            Format = Description.Format.ToVulkan(),
            SubresourceRange = new ImageSubresourceRange(Aspect, 0, (uint)mipLevels, 0, 1),
        };
        VulkanResult.Check(_device.Vk.CreateImageView(_device.Handle, &info, null, out ImageView view));
        return view;
    }

    private static ImageUsageFlags GetUsage(TextureUsage usage)
    {
        ImageUsageFlags flags = 0;
        if (usage.HasFlag(TextureUsage.ShaderRead))
        {
            flags |= ImageUsageFlags.SampledBit;
        }

        if (usage.HasFlag(TextureUsage.ShaderWrite))
        {
            flags |= ImageUsageFlags.StorageBit;
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
