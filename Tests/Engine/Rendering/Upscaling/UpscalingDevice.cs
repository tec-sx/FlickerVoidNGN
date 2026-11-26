using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Presentation;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;

namespace FlickerVoid.Engine.Rendering.Tests.Upscaling;

/// <summary>
/// A device that has DLSS, and Ray Reconstruction where given it, through <see cref="StandInUpscaler"/>s, and draws everything
/// else with the device it wraps.
/// </summary>
internal sealed class UpscalingDevice(IGraphicsDevice device, GraphicsFeatures upscaling) : IGraphicsDevice
{
    /// <summary>The upscalers it made, in order.</summary>
    public List<StandInUpscaler> Upscalers { get; } = [];

    public string AdapterName => device.AdapterName;

    public GraphicsFeatures Features => device.Features | upscaling;

    public GraphicsInfo GetInfo() => device.GetInfo();

    public IBuffer CreateBuffer(in BufferDescription description, ReadOnlySpan<byte> initialData = default) => device.CreateBuffer(description, initialData);

    public ITexture CreateTexture(in TextureDescription description, ReadOnlySpan<byte> initialData = default) => device.CreateTexture(description, initialData);

    public ISampler CreateSampler(in SamplerDescription description) => device.CreateSampler(description);

    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDescription description) => device.CreateGraphicsPipeline(description);

    public IPipeline CreateComputePipeline(ComputePipelineDescription description) => device.CreateComputePipeline(description);

    public IAccelerationStructure CreateBottomLevel(in BottomLevelDescription description) => device.CreateBottomLevel(description);

    public IAccelerationStructure CreateTopLevel(in TopLevelDescription description) => device.CreateTopLevel(description);

    public IUpscaler CreateUpscaler(in UpscalerDescription description)
    {
        StandInUpscaler upscaler = new(device, description);
        Upscalers.Add(upscaler);
        return upscaler;
    }

    public ICommandList CreateCommandList() => device.CreateCommandList();

    public ISwapchain CreateSwapchain(in SwapchainDescription description) => device.CreateSwapchain(description);

    public void Submit(ICommandList commandList) => device.Submit(commandList);

    public void WaitIdle() => device.WaitIdle();

    public void Dispose() => device.Dispose();
}
