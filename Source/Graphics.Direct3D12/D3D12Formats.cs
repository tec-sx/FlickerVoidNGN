using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using D3D12CullMode = Silk.NET.Direct3D12.CullMode;
using D3D12Filter = Silk.NET.Direct3D12.Filter;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>Converts engine enums to Direct3D 12 ones.</summary>
internal static class D3D12Formats
{
    /// <summary><see cref="PixelFormat"/> values are DXGI's.</summary>
    public static Format ToDxgi(this PixelFormat format) => (Format)(int)format;

    /// <summary>The resource format: depth formats shaders read are typeless, so a depth view and a shader view can both be made.</summary>
    public static Format ToResourceFormat(this PixelFormat format, bool shaderRead)
    {
        if (!shaderRead || !format.IsDepth())
        {
            return format.ToDxgi();
        }

        switch (format)
        {
            case PixelFormat.Depth32Float:
                return Format.FormatR32Typeless;
            case PixelFormat.Depth24UNormStencil8:
                return Format.FormatR24G8Typeless;
            case PixelFormat.Depth16UNorm:
                return Format.FormatR16Typeless;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <summary>The format shaders read a texture with.</summary>
    public static Format ToShaderReadFormat(this PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.Depth32Float:
                return Format.FormatR32Float;
            case PixelFormat.Depth24UNormStencil8:
                return Format.FormatR24UnormX8Typeless;
            case PixelFormat.Depth16UNorm:
                return Format.FormatR16Unorm;
            default:
                return format.ToDxgi();
        }
    }

    public static PrimitiveTopologyType ToTopologyType(this PrimitiveTopology topology)
    {
        if (topology == PrimitiveTopology.LineList)
        {
            return PrimitiveTopologyType.Line;
        }

        return PrimitiveTopologyType.Triangle;
    }

    public static D3DPrimitiveTopology ToD3D12(this PrimitiveTopology topology)
    {
        switch (topology)
        {
            case PrimitiveTopology.TriangleList:
                return D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist;
            case PrimitiveTopology.TriangleStrip:
                return D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglestrip;
            case PrimitiveTopology.LineList:
                return D3DPrimitiveTopology.D3DPrimitiveTopologyLinelist;
            default:
                throw new ArgumentOutOfRangeException(nameof(topology), topology, null);
        }
    }

    public static D3D12CullMode ToD3D12(this CullMode cullMode)
    {
        switch (cullMode)
        {
            case CullMode.None:
                return D3D12CullMode.None;
            case CullMode.Front:
                return D3D12CullMode.Front;
            case CullMode.Back:
                return D3D12CullMode.Back;
            default:
                throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, null);
        }
    }

    public static ComparisonFunc ToD3D12(this CompareFunction compare)
    {
        switch (compare)
        {
            case CompareFunction.Never:
                return ComparisonFunc.Never;
            case CompareFunction.Less:
                return ComparisonFunc.Less;
            case CompareFunction.Equal:
                return ComparisonFunc.Equal;
            case CompareFunction.LessEqual:
                return ComparisonFunc.LessEqual;
            case CompareFunction.Greater:
                return ComparisonFunc.Greater;
            case CompareFunction.NotEqual:
                return ComparisonFunc.NotEqual;
            case CompareFunction.GreaterEqual:
                return ComparisonFunc.GreaterEqual;
            case CompareFunction.Always:
                return ComparisonFunc.Always;
            default:
                throw new ArgumentOutOfRangeException(nameof(compare), compare, null);
        }
    }

    public static D3D12Filter ToD3D12(this SamplerFilter filter)
    {
        if (filter == SamplerFilter.Linear)
        {
            return D3D12Filter.MinMagMipLinear;
        }

        return D3D12Filter.MinMagMipPoint;
    }

    public static TextureAddressMode ToD3D12(this SamplerAddressMode addressMode)
    {
        switch (addressMode)
        {
            case SamplerAddressMode.Wrap:
                return TextureAddressMode.Wrap;
            case SamplerAddressMode.Mirror:
                return TextureAddressMode.Mirror;
            case SamplerAddressMode.Clamp:
                return TextureAddressMode.Clamp;
            default:
                throw new ArgumentOutOfRangeException(nameof(addressMode), addressMode, null);
        }
    }

    public static Format ToDxgi(this IndexFormat format)
    {
        if (format == IndexFormat.UInt32)
        {
            return Format.FormatR32Uint;
        }

        return Format.FormatR16Uint;
    }

    public static RenderTargetBlendDesc ToD3D12(this BlendMode blend)
    {
        RenderTargetBlendDesc state = new()
        {
            SrcBlend = Blend.One,
            DestBlend = Blend.Zero,
            BlendOp = BlendOp.Add,
            SrcBlendAlpha = Blend.One,
            DestBlendAlpha = Blend.InvSrcAlpha,
            BlendOpAlpha = BlendOp.Add,
            LogicOp = LogicOp.Noop,
            RenderTargetWriteMask = (byte)ColorWriteEnable.All,
        };

        switch (blend)
        {
            case BlendMode.Opaque:
                return state;
            case BlendMode.AlphaBlend:
                state.BlendEnable = true;
                state.SrcBlend = Blend.SrcAlpha;
                state.DestBlend = Blend.InvSrcAlpha;
                return state;
            case BlendMode.Premultiplied:
                state.BlendEnable = true;
                state.SrcBlend = Blend.One;
                state.DestBlend = Blend.InvSrcAlpha;
                return state;
            case BlendMode.Additive:
                state.BlendEnable = true;
                state.SrcBlend = Blend.One;
                state.DestBlend = Blend.One;
                state.SrcBlendAlpha = Blend.One;
                state.DestBlendAlpha = Blend.One;
                return state;
            default:
                throw new ArgumentOutOfRangeException(nameof(blend), blend, null);
        }
    }
}
