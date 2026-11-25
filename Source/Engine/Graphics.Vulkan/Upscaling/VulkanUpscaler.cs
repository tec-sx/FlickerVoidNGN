using Silk.NET.Vulkan;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Graphics.Vulkan.Commands;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>
/// DLSS upscaling to one output size: its feature is made in the command buffer of the first frame it upscales, and the exposure
/// the tonemapper multiplies by is cleared into a 1x1 image each frame, which DLSS reads.
/// </summary>
internal sealed unsafe class VulkanUpscaler : IUpscaler
{
    private readonly VulkanGraphicsDevice _device;
    private readonly VulkanDlss _dlss;
    private readonly VulkanTexture _exposure;
    private nint _feature;
    private bool _disposed;

    public VulkanUpscaler(VulkanGraphicsDevice device, VulkanDlss dlss, in UpscalerDescription description)
    {
        _device = device;
        _dlss = dlss;
        Description = description;
        (RenderWidth, RenderHeight) = dlss.GetRenderSize(description.Mode, description.OutputWidth, description.OutputHeight);
        TextureDescription exposure = new(1, 1, PixelFormat.R16Float, TextureUsage.ShaderRead | TextureUsage.CopyDestination) { Name = "DLSS exposure" };
        _exposure = new VulkanTexture(device, exposure, 0);
    }

    public UpscalerDescription Description { get; }

    public int RenderWidth { get; }

    public int RenderHeight { get; }

    public void Upscale(ICommandList commands, in UpscaleInputs inputs)
    {
        VulkanCommandList vulkanCommands = (VulkanCommandList)commands;
        CommandBuffer buffer = vulkanCommands.Handle;
        if (_feature == 0)
        {
            _feature = _dlss.CreateFeature(buffer, Description.Mode, RenderWidth, RenderHeight, Description.OutputWidth, Description.OutputHeight);
        }

        WriteExposure(vulkanCommands, inputs.Exposure);
        DlssFrame frame = new()
        {
            Color = GetImage(inputs.Color),
            Depth = GetImage(inputs.Depth),
            Motion = GetImage(inputs.Motion),
            Exposure = GetImage(_exposure),
            Output = GetImage(inputs.Output),
            JitterX = inputs.Jitter.X,
            JitterY = inputs.Jitter.Y,
            RenderWidth = (uint)RenderWidth,
            RenderHeight = (uint)RenderHeight,
            Reset = inputs.Reset ? 1 : 0,
        };
        _dlss.Evaluate(buffer, _feature, frame);

        // NGX leaves its own pipeline and descriptor sets bound; the passes after it bind their pipelines and push their constants.
        _device.Bindless.Bind(buffer, PipelineBindPoint.Compute);
        _device.Bindless.Bind(buffer, PipelineBindPoint.Graphics);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _exposure.Dispose();
        nint feature = _feature;
        if (feature != 0)
        {
            VulkanDlss dlss = _dlss;
            _device.ReleaseLater(() => dlss.ReleaseFeature(feature));
        }
    }

    private static DlssImage GetImage(ITexture texture)
    {
        VulkanTexture vulkanTexture = (VulkanTexture)texture;
        TextureDescription description = vulkanTexture.Description;
        Format format = description.Format.ToVulkan();
        return new DlssImage
        {
            Image = vulkanTexture.Handle.Handle,
            View = vulkanTexture.View.Handle,
            Format = (uint)format,
            Aspect = (uint)vulkanTexture.Aspect,
            Width = (uint)description.Width,
            Height = (uint)description.Height,
        };
    }

    private void WriteExposure(VulkanCommandList commands, float exposure)
    {
        commands.Barrier(_exposure, ResourceState.CopyDestination);
        ClearColorValue value = new(exposure, 0, 0, 0);
        ImageSubresourceRange range = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
        _device.Vk.CmdClearColorImage(commands.Handle, _exposure.Handle, ImageLayout.TransferDstOptimal, &value, 1, &range);
        commands.Barrier(_exposure, ResourceState.ShaderRead);
    }
}
