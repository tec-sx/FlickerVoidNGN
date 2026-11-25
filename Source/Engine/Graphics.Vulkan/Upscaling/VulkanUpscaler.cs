using Silk.NET.Vulkan;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Graphics.Vulkan.Commands;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Textures;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>
/// DLSS upscaling to one output size, or Ray Reconstruction denoising as it upscales: its feature is made in the command buffer of
/// the first frame it upscales. Plain DLSS reads the exposure the tonemapper multiplies by, cleared into a 1x1 image each frame;
/// Ray Reconstruction takes none, and reads the frame's guides instead.
/// </summary>
internal sealed unsafe class VulkanUpscaler : IUpscaler
{
    private readonly VulkanGraphicsDevice _device;
    private readonly VulkanDlss _dlss;
    private readonly VulkanTexture? _exposure;
    private nint _feature;
    private bool _disposed;

    public VulkanUpscaler(VulkanGraphicsDevice device, VulkanDlss dlss, in UpscalerDescription description)
    {
        _device = device;
        _dlss = dlss;
        Description = description;
        (RenderWidth, RenderHeight) = dlss.GetRenderSize(description.Mode, description.Denoises, description.OutputWidth, description.OutputHeight);
        if (!description.Denoises)
        {
            TextureDescription exposure = new(1, 1, PixelFormat.R16Float, TextureUsage.ShaderRead | TextureUsage.CopyDestination) { Name = "DLSS exposure" };
            _exposure = new VulkanTexture(device, exposure, 0);
        }
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
            _feature = _dlss.CreateFeature(buffer, Description.Mode, Description.Denoises, RenderWidth, RenderHeight, Description.OutputWidth, Description.OutputHeight);
        }

        DlssFrame frame = new()
        {
            Color = GetImage(inputs.Color),
            Depth = GetImage(inputs.Depth),
            Motion = GetImage(inputs.Motion),
            Output = GetImage(inputs.Output),
            JitterX = inputs.Jitter.X,
            JitterY = inputs.Jitter.Y,
            RenderWidth = (uint)RenderWidth,
            RenderHeight = (uint)RenderHeight,
            Reset = inputs.Reset ? 1 : 0,
        };
        if (_exposure is null)
        {
            frame.Guides = GetGuides(inputs);
        }
        else
        {
            WriteExposure(vulkanCommands, _exposure, inputs.Exposure);
            frame.Exposure = GetImage(_exposure);
        }

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
        _exposure?.Dispose();
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

    private static DlssGuides GetGuides(in UpscaleInputs inputs)
    {
        if (inputs.Guides is not { } guides)
        {
            throw new ArgumentException("An upscaler that denoises reads the frame's guides beside it.", nameof(inputs));
        }

        return new DlssGuides
        {
            DiffuseAlbedo = GetImage(guides.DiffuseAlbedo),
            SpecularAlbedo = GetImage(guides.SpecularAlbedo),
            NormalRoughness = GetImage(guides.NormalRoughness),
            WorldToView = guides.WorldToView,
            ViewToClip = guides.ViewToClip,
        };
    }

    private void WriteExposure(VulkanCommandList commands, VulkanTexture texture, float exposure)
    {
        commands.Barrier(texture, ResourceState.CopyDestination);
        ClearColorValue value = new(exposure, 0, 0, 0);
        ImageSubresourceRange range = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
        _device.Vk.CmdClearColorImage(commands.Handle, texture.Handle, ImageLayout.TransferDstOptimal, &value, 1, &range);
        commands.Barrier(texture, ResourceState.ShaderRead);
    }
}
