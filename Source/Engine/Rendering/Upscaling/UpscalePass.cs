using System.Numerics;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Engine.Rendering.Upscaling;

/// <summary>
/// Fills the output's size from the lit scene with the renderer's upscaler, after the depth fade and before post-processing, and
/// carries the scene's alpha (its paint's coverage and thickness) over, which the upscaler leaves out (Shaders/Upscale.hlsl);
/// does nothing while the scene is drawn at the output's size. For an upscaler that denoises, it first draws the guides of the
/// frame's surfaces (Shaders/DenoiseGuides.hlsl), whose textures exist only while one does.
/// </summary>
internal sealed class UpscalePass : IRenderPass
{
    private const int GroupSize = 8;

    private readonly IGraphicsDevice _device;
    private readonly ShaderLibrary _shaders;
    private readonly IPipeline _mask;
    private IPipeline? _guide;
    private ITexture? _diffuseAlbedo;
    private ITexture? _specularAlbedo;
    private ITexture? _normalRoughness;

    public UpscalePass(IGraphicsDevice device, ShaderLibrary shaders)
    {
        _device = device;
        _shaders = shaders;
        ComputePipelineDescription description = new() { ComputeShader = shaders.Load("Upscale", "MaskMain"), Name = "Upscale mask" };
        _mask = device.CreateComputePipeline(description);
    }

    public void Prepare(RenderContext context)
    {
        if (context.Denoises)
        {
            EnsureGuides(context.RenderWidth, context.RenderHeight);
        }
        else
        {
            ReleaseGuides();
        }
    }

    public void Execute(RenderContext context)
    {
        if (context.Upscaler is not { } upscaler)
        {
            return;
        }

        ICommandList commands = context.Commands;
        ScreenTargets targets = context.Targets;
        ITexture output = targets.Upscaled!;
        ITexture motion = targets.GBuffer.Motion;
        commands.Barrier(targets.Hdr, ResourceState.ShaderRead);
        commands.Barrier(targets.Depth, ResourceState.ShaderRead);
        commands.Barrier(motion, ResourceState.ShaderRead);
        commands.Barrier(output, ResourceState.ShaderWrite);
        UpscaleInputs inputs = new(targets.Hdr, targets.Depth, motion, output, context.Jitter, context.Mood.Exposure, context.ResetsHistory);
        if (context.Denoises)
        {
            DrawGuides(context);
            DenoiseGuides guides = new(_diffuseAlbedo!, _specularAlbedo!, _normalRoughness!, context.View.View, context.View.Projection);
            inputs = inputs with { Guides = guides };
        }

        upscaler.Upscale(commands, inputs);

        commands.Barrier(output, ResourceState.ShaderWrite);
        commands.SetPipeline(_mask);
        Vector2 renderSize = new(targets.Width, targets.Height);
        UpscaleMaskConstants constants = new(targets.Hdr.BindlessIndex, output.BindlessIndex, context.ClampSampler.BindlessIndex, 0, context.Jitter, renderSize);
        commands.PushConstants(constants);
        commands.Dispatch((targets.OutputWidth + GroupSize - 1) / GroupSize, (targets.OutputHeight + GroupSize - 1) / GroupSize);
        commands.Barrier(output, ResourceState.ShaderRead);
    }

    public void Dispose()
    {
        ReleaseGuides();
        _guide?.Dispose();
        _mask.Dispose();
    }

    private void DrawGuides(RenderContext context)
    {
        ICommandList commands = context.Commands;
        GBuffer gBuffer = context.GBuffer;
        ITexture diffuseAlbedo = _diffuseAlbedo!;
        ITexture specularAlbedo = _specularAlbedo!;
        ITexture normalRoughness = _normalRoughness!;
        commands.Barrier(gBuffer.Albedo, ResourceState.ShaderRead);
        commands.Barrier(gBuffer.Normal, ResourceState.ShaderRead);
        commands.Barrier(gBuffer.Material, ResourceState.ShaderRead);
        commands.Barrier(diffuseAlbedo, ResourceState.ShaderWrite);
        commands.Barrier(specularAlbedo, ResourceState.ShaderWrite);
        commands.Barrier(normalRoughness, ResourceState.ShaderWrite);
        commands.SetPipeline(_guide!);
        DenoiseGuideConstants constants = new(context.FrameBuffer, diffuseAlbedo.BindlessIndex, specularAlbedo.BindlessIndex, normalRoughness.BindlessIndex);
        commands.PushConstants(constants);
        commands.Dispatch((context.RenderWidth + GroupSize - 1) / GroupSize, (context.RenderHeight + GroupSize - 1) / GroupSize);
        commands.Barrier(diffuseAlbedo, ResourceState.ShaderRead);
        commands.Barrier(specularAlbedo, ResourceState.ShaderRead);
        commands.Barrier(normalRoughness, ResourceState.ShaderRead);
    }

    /// <summary>Creates the guides' pipeline the first time an upscaler denoises, and their textures at the frame's size.</summary>
    private void EnsureGuides(int width, int height)
    {
        if (_guide is null)
        {
            ComputePipelineDescription guide = new() { ComputeShader = _shaders.Load("DenoiseGuides", "ComputeMain"), Name = "Denoise guides" };
            _guide = _device.CreateComputePipeline(guide);
        }

        if (_diffuseAlbedo is { } existing && existing.Description.Width == width && existing.Description.Height == height)
        {
            return;
        }

        ReleaseGuides();
        _diffuseAlbedo = CreateGuide(width, height, "Diffuse albedo guide");
        _specularAlbedo = CreateGuide(width, height, "Specular albedo guide");
        _normalRoughness = CreateGuide(width, height, "Normal and roughness guide");
    }

    /// <summary>A guide, which can also be copied out, as the G-buffer's targets can, to be looked at.</summary>
    private ITexture CreateGuide(int width, int height, string name)
    {
        TextureUsage usage = TextureUsage.ShaderRead | TextureUsage.ShaderWrite | TextureUsage.CopySource;
        TextureDescription description = new(width, height, PixelFormat.Rgba16Float, usage) { Name = name };
        return _device.CreateTexture(description);
    }

    private void ReleaseGuides()
    {
        _diffuseAlbedo?.Dispose();
        _diffuseAlbedo = null;
        _specularAlbedo?.Dispose();
        _specularAlbedo = null;
        _normalRoughness?.Dispose();
        _normalRoughness = null;
    }
}
