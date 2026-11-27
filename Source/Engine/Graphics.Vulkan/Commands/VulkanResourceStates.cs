using Silk.NET.Vulkan;

using FlickerVoid.Engine.Graphics.Commands;

namespace FlickerVoid.Engine.Graphics.Vulkan.Commands;

/// <summary>The image layout, pipeline stages and memory accesses each <see cref="ResourceState"/> stands for.</summary>
internal readonly record struct VulkanResourceStates(ImageLayout Layout, PipelineStageFlags2 Stages, AccessFlags2 Access)
{
    private const PipelineStageFlags2 ShaderStages = PipelineStageFlags2.VertexShaderBit | PipelineStageFlags2.FragmentShaderBit | PipelineStageFlags2.ComputeShaderBit;
    private const PipelineStageFlags2 DepthStages = PipelineStageFlags2.EarlyFragmentTestsBit | PipelineStageFlags2.LateFragmentTestsBit;

    public static VulkanResourceStates For(ResourceState state)
    {
        switch (state)
        {
            case ResourceState.Undefined:
                return new(ImageLayout.Undefined, PipelineStageFlags2.None, AccessFlags2.None);
            case ResourceState.CopySource:
                return new(ImageLayout.TransferSrcOptimal, PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferReadBit);
            case ResourceState.CopyDestination:
                return new(ImageLayout.TransferDstOptimal, PipelineStageFlags2.AllTransferBit, AccessFlags2.TransferWriteBit);
            case ResourceState.ShaderRead:
                return new(ImageLayout.ShaderReadOnlyOptimal, ShaderStages, AccessFlags2.ShaderReadBit);
            case ResourceState.ShaderWrite:
                return new(ImageLayout.General, ShaderStages, AccessFlags2.ShaderStorageReadBit | AccessFlags2.ShaderStorageWriteBit);
            case ResourceState.RenderTarget:
                return new(ImageLayout.ColorAttachmentOptimal, PipelineStageFlags2.ColorAttachmentOutputBit, AccessFlags2.ColorAttachmentReadBit | AccessFlags2.ColorAttachmentWriteBit);
            case ResourceState.DepthWrite:
                return new(ImageLayout.DepthStencilAttachmentOptimal, DepthStages, AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.DepthStencilAttachmentWriteBit);
            case ResourceState.DepthRead:
                return new(ImageLayout.DepthStencilReadOnlyOptimal, DepthStages | ShaderStages, AccessFlags2.DepthStencilAttachmentReadBit | AccessFlags2.ShaderReadBit);
            case ResourceState.IndexBuffer:
                return new(ImageLayout.Undefined, PipelineStageFlags2.IndexInputBit, AccessFlags2.IndexReadBit);
            case ResourceState.IndirectArgument:
                return new(ImageLayout.Undefined, PipelineStageFlags2.DrawIndirectBit, AccessFlags2.IndirectCommandReadBit);
            case ResourceState.Present:
                return new(ImageLayout.PresentSrcKhr, PipelineStageFlags2.None, AccessFlags2.None);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }
}

