using Silk.NET.Vulkan;
using VkPrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using VkSamplerAddressMode = Silk.NET.Vulkan.SamplerAddressMode;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>Converts engine enums to Vulkan ones.</summary>
internal static class VulkanFormats
{
    public static Format ToVulkan(this PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.None:
                return Format.Undefined;
            case PixelFormat.Rgba32Float:
                return Format.R32G32B32A32Sfloat;
            case PixelFormat.Rgba16Float:
                return Format.R16G16B16A16Sfloat;
            case PixelFormat.Rg11B10Float:
                return Format.B10G11R11UfloatPack32;
            case PixelFormat.Rgba8UNorm:
                return Format.R8G8B8A8Unorm;
            case PixelFormat.Rgba8UNormSrgb:
                return Format.R8G8B8A8Srgb;
            case PixelFormat.Rg16Float:
                return Format.R16G16Sfloat;
            case PixelFormat.Depth32Float:
                return Format.D32Sfloat;
            case PixelFormat.R32Float:
                return Format.R32Sfloat;
            case PixelFormat.Depth24UNormStencil8:
                return Format.D24UnormS8Uint;
            case PixelFormat.R16Float:
                return Format.R16Sfloat;
            case PixelFormat.Depth16UNorm:
                return Format.D16Unorm;
            case PixelFormat.R8UNorm:
                return Format.R8Unorm;
            case PixelFormat.Bgra8UNorm:
                return Format.B8G8R8A8Unorm;
            case PixelFormat.Bgra8UNormSrgb:
                return Format.B8G8R8A8Srgb;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    public static ImageAspectFlags GetAspect(this PixelFormat format)
    {
        if (format.HasStencil())
        {
            return ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit;
        }

        if (format.IsDepth())
        {
            return ImageAspectFlags.DepthBit;
        }

        return ImageAspectFlags.ColorBit;
    }

    public static VkPrimitiveTopology ToVulkan(this PrimitiveTopology topology)
    {
        switch (topology)
        {
            case PrimitiveTopology.TriangleList:
                return VkPrimitiveTopology.TriangleList;
            case PrimitiveTopology.TriangleStrip:
                return VkPrimitiveTopology.TriangleStrip;
            case PrimitiveTopology.LineList:
                return VkPrimitiveTopology.LineList;
            default:
                throw new ArgumentOutOfRangeException(nameof(topology), topology, null);
        }
    }

    public static CullModeFlags ToVulkan(this CullMode cullMode)
    {
        switch (cullMode)
        {
            case CullMode.None:
                return CullModeFlags.None;
            case CullMode.Front:
                return CullModeFlags.FrontBit;
            case CullMode.Back:
                return CullModeFlags.BackBit;
            default:
                throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, null);
        }
    }

    public static CompareOp ToVulkan(this CompareFunction compare)
    {
        switch (compare)
        {
            case CompareFunction.Never:
                return CompareOp.Never;
            case CompareFunction.Less:
                return CompareOp.Less;
            case CompareFunction.Equal:
                return CompareOp.Equal;
            case CompareFunction.LessEqual:
                return CompareOp.LessOrEqual;
            case CompareFunction.Greater:
                return CompareOp.Greater;
            case CompareFunction.NotEqual:
                return CompareOp.NotEqual;
            case CompareFunction.GreaterEqual:
                return CompareOp.GreaterOrEqual;
            case CompareFunction.Always:
                return CompareOp.Always;
            default:
                throw new ArgumentOutOfRangeException(nameof(compare), compare, null);
        }
    }

    public static Filter ToVulkan(this SamplerFilter filter)
    {
        if (filter == SamplerFilter.Linear)
        {
            return Filter.Linear;
        }

        return Filter.Nearest;
    }

    public static SamplerMipmapMode ToVulkanMipmap(this SamplerFilter filter)
    {
        if (filter == SamplerFilter.Linear)
        {
            return SamplerMipmapMode.Linear;
        }

        return SamplerMipmapMode.Nearest;
    }

    public static VkSamplerAddressMode ToVulkan(this SamplerAddressMode addressMode)
    {
        switch (addressMode)
        {
            case SamplerAddressMode.Wrap:
                return VkSamplerAddressMode.Repeat;
            case SamplerAddressMode.Mirror:
                return VkSamplerAddressMode.MirroredRepeat;
            case SamplerAddressMode.Clamp:
                return VkSamplerAddressMode.ClampToEdge;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressMode), addressMode, null);
        }
    }

    public static AttachmentLoadOp ToVulkan(this LoadAction load)
    {
        switch (load)
        {
            case LoadAction.Load:
                return AttachmentLoadOp.Load;
            case LoadAction.Clear:
                return AttachmentLoadOp.Clear;
            case LoadAction.DontCare:
                return AttachmentLoadOp.DontCare;
            default:
                throw new ArgumentOutOfRangeException(nameof(load), load, null);
        }
    }

    public static IndexType ToVulkan(this IndexFormat format)
    {
        if (format == IndexFormat.UInt32)
        {
            return IndexType.Uint32;
        }

        return IndexType.Uint16;
    }

    public static PipelineColorBlendAttachmentState ToVulkan(this BlendMode blend)
    {
        PipelineColorBlendAttachmentState state = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            ColorBlendOp = BlendOp.Add,
            AlphaBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
        };

        switch (blend)
        {
            case BlendMode.Opaque:
                return state;
            case BlendMode.AlphaBlend:
                state.BlendEnable = true;
                state.SrcColorBlendFactor = BlendFactor.SrcAlpha;
                state.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
                return state;
            case BlendMode.Premultiplied:
                state.BlendEnable = true;
                state.SrcColorBlendFactor = BlendFactor.One;
                state.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
                return state;
            case BlendMode.Additive:
                state.BlendEnable = true;
                state.SrcColorBlendFactor = BlendFactor.One;
                state.DstColorBlendFactor = BlendFactor.One;
                state.SrcAlphaBlendFactor = BlendFactor.One;
                state.DstAlphaBlendFactor = BlendFactor.One;
                return state;
            default:
                throw new ArgumentOutOfRangeException(nameof(blend), blend, null);
        }
    }
}
