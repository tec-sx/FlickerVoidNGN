using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using D3D12ShaderBytecode = Silk.NET.Direct3D12.ShaderBytecode;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>A graphics pipeline state on the bindless root signature.</summary>
internal sealed unsafe class D3D12Pipeline : IPipeline
{
    /// <summary>The most render targets a pipeline writes.</summary>
    private const int MaxColorTargets = 8;

    private readonly D3D12GraphicsDevice _device;
    private bool _disposed;

    public D3D12Pipeline(D3D12GraphicsDevice device, GraphicsPipelineDescription description)
    {
        _device = device;
        Topology = description.Topology.ToD3D12();
        int colorCount = description.ColorFormats.Count;
        if (colorCount > MaxColorTargets)
        {
            throw new ArgumentException($"A pipeline writes at most {MaxColorTargets} color targets; this one has {colorCount}.", nameof(description));
        }

        fixed (byte* vertex = description.VertexShader.Code)
        fixed (byte* pixel = description.PixelShader.Code)
        {
            GraphicsPipelineStateDesc state = new()
            {
                PRootSignature = device.Bindless.RootSignature,
                VS = new D3D12ShaderBytecode { PShaderBytecode = vertex, BytecodeLength = (nuint)description.VertexShader.Code.Length },
                PS = new D3D12ShaderBytecode { PShaderBytecode = pixel, BytecodeLength = (nuint)description.PixelShader.Code.Length },
                SampleMask = uint.MaxValue,
                RasterizerState = CreateRasterizer(description.CullMode),
                DepthStencilState = CreateDepthStencil(description),
                PrimitiveTopologyType = description.Topology.ToTopologyType(),
                NumRenderTargets = (uint)colorCount,
                DSVFormat = description.DepthFormat.ToDxgi(),
                SampleDesc = new SampleDesc(1, 0),
            };

            RenderTargetBlendDesc blend = description.Blend.ToD3D12();
            for (int index = 0; index < colorCount; index++)
            {
                state.RTVFormats[index] = description.ColorFormats[index].ToDxgi();
                state.BlendState.RenderTarget[index] = blend;
            }

            Guid pipelineId = ID3D12PipelineState.Guid;
            ID3D12PipelineState* pipeline = null;
            int result = device.Handle->CreateGraphicsPipelineState(&state, &pipelineId, (void**)&pipeline);
            device.ReportMessages();
            D3D12Com.Check(result);
            Handle = pipeline;
        }

        device.SetName((ID3D12Object*)Handle, description.Name);
    }

    public ID3D12PipelineState* Handle { get; }

    public D3DPrimitiveTopology Topology { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ID3D12PipelineState* handle = Handle;
        _device.ReleaseLater(() => D3D12Com.Release(handle));
    }

    /// <summary>Front faces are clockwise, as in the Vulkan backend.</summary>
    private static RasterizerDesc CreateRasterizer(CullMode cullMode)
    {
        return new RasterizerDesc
        {
            FillMode = FillMode.Solid,
            CullMode = cullMode.ToD3D12(),
            FrontCounterClockwise = false,
            DepthClipEnable = true,
        };
    }

    private static DepthStencilDesc CreateDepthStencil(GraphicsPipelineDescription description)
    {
        DepthStencilDesc depth = new()
        {
            DepthEnable = description.Depth.Test,
            DepthWriteMask = DepthWriteMask.Zero,
            DepthFunc = description.Depth.Compare.ToD3D12(),
        };
        if (description.Depth.Write)
        {
            depth.DepthWriteMask = DepthWriteMask.All;
        }

        return depth;
    }
}
