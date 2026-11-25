using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Presentation;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;

namespace FlickerVoid.Engine.Graphics.Devices;

/// <summary>A GPU and the queue work is submitted to. Create resources and command lists from it, and dispose them before it.</summary>
public interface IGraphicsDevice : IDisposable
{
    string AdapterName { get; }

    /// <summary>What it can do beyond what every device does: what the GPU has, less what its options left out.</summary>
    GraphicsFeatures Features { get; }

    /// <summary>The adapter, the API version and the memory the device's resources hold now.</summary>
    GraphicsInfo GetInfo();

    /// <summary>Creates a buffer; initial data is copied in before the call returns, leaving a GPU buffer in its shader-read or index state.</summary>
    IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default);

    /// <summary>Creates a texture; initial data (tightly packed rows of the full-size level) is copied in before the call returns and the other mip levels are filtered from it, leaving it in its shader-read state.</summary>
    ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default);

    ISampler CreateSampler(in SamplerDescription description);

    IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description);

    IPipeline CreateComputePipeline(ComputePipelineDescription description);

    /// <summary>
    /// Creates a bottom-level acceleration structure big enough for the triangles, to build with
    /// <see cref="ICommandList.BuildBottomLevels"/>; throws <see cref="NotSupportedException"/> without <see cref="GraphicsFeatures.RayQueries"/>.
    /// </summary>
    IAccelerationStructure CreateBottomLevel(in BottomLevelDescription description);

    /// <summary>
    /// Creates a top-level acceleration structure big enough for the instances, to build with <see cref="ICommandList.BuildTopLevel"/>;
    /// throws <see cref="NotSupportedException"/> without <see cref="GraphicsFeatures.RayQueries"/>.
    /// </summary>
    IAccelerationStructure CreateTopLevel(in TopLevelDescription description);

    ICommandList CreateCommandList();

    /// <summary>Creates a swapchain for the window; throws <see cref="GraphicsUnavailableException"/> when the device can't present to it.</summary>
    ISwapchain CreateSwapchain(in SwapchainDescription description);

    void Submit(ICommandList commandList);

    /// <summary>Blocks until the GPU has finished every submitted command list.</summary>
    void WaitIdle();
}

