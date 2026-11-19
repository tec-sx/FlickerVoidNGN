using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Shadows;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Draws a frame: extracts the world into a <see cref="RenderScene"/>, lets every pass of the <see cref="Pipeline"/> prepare,
/// uploads the frame's view, lights, shadows, clusters and probes, then records the passes in order. The default passes skin
/// meshes, gather lights, draw the shadow atlas, a depth prepass, screen shadows, the lit HDR scene, and post-processing
/// graded as the <see cref="Mood"/> says.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    public const PixelFormat DepthFormat = PixelFormat.Depth32Float;

    /// <summary>
    /// How many frames the GPU may still be drawing when <see cref="Render(ICommandList, World, ITexture)"/> is called: each
    /// frame writes its data to its own buffer, reused this many frames later.
    /// </summary>
    public const int MaxFramesInFlight = 3;

    private static readonly Vector4 Background = new(0, 0, 0, 1);

    private readonly ScreenTargets _targets;
    private readonly ITexture _white;
    private readonly ISampler _sampler;
    private readonly ISampler _clampSampler;
    private readonly TextureLibrary _textures;
    private readonly ProbeCache _probes;
    private readonly FrameDataWriter _frameData;
    private readonly RenderScene _scene = new();
    private readonly RenderContext _context;

    public SceneRenderer(IGraphicsDevice device, IContentManager content, PixelFormat colorFormat)
    {
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Rendering");
        _targets = new ScreenTargets(device);
        TextureDescription whiteDescription = new(1, 1, PixelFormat.Rgba8UNorm, TextureUsage.ShaderRead) { Name = "White" };
        _white = device.CreateTexture(whiteDescription, [255, 255, 255, 255]);
        SamplerDescription samplerDescription = new(SamplerFilter.Linear, SamplerAddressMode.Wrap);
        _sampler = device.CreateSampler(samplerDescription);
        SamplerDescription clampDescription = new(SamplerFilter.Linear, SamplerAddressMode.Clamp);
        _clampSampler = device.CreateSampler(clampDescription);
        _textures = new TextureLibrary(device, content);
        _probes = new ProbeCache(device, content);
        _frameData = new FrameDataWriter(device, _probes, MaxFramesInFlight);
        _context = new RenderContext(_scene)
        {
            Targets = _targets,
            Textures = _textures,
            Sampler = _sampler,
            ClampSampler = _clampSampler,
            White = _white,
        };

        Pipeline.Add(new SkinningPass(device, shaders, content));
        Pipeline.Add(new LightGatherPass());
        Pipeline.Add(new ShadowPass(device, shaders));
        Pipeline.Add(new DepthPrepass(device, shaders));
        Pipeline.Add(new ScreenShadowPass(device, shaders));
        Pipeline.Add(new OpaquePass(device, shaders));
        Pipeline.Add(new PostProcessPass(device, shaders, colorFormat));
    }

    /// <summary>The passes that draw each frame, in order; add, insert or replace passes to change how frames are drawn.</summary>
    public RenderPipeline Pipeline { get; } = new();

    /// <summary>Turns the game world into the render scene; replace it to draw something other than its standard components.</summary>
    public IRenderExtractor Extractor { get; set; } = new WorldExtractor();

    /// <summary>How the scene is lit and graded; replace it to change the mood.</summary>
    public LightingMood Mood { get; set; } = new();

    /// <summary>Whether point and spot lights light the scene; switch it off to judge the sun and ambient light alone.</summary>
    public bool LocalLights { get; set; } = true;

    /// <summary>Whether the irradiance volume's probes add bounced light.</summary>
    public bool Probes { get; set; } = true;

    /// <summary>Probes that replace the world's irradiance volume while a bake renders, or null.</summary>
    internal ProbeSource? BakeProbes { get; set; }

    /// <summary>Draws the world from its first camera into the color target, or clears it without one; leaves it in the RenderTarget state.</summary>
    public void Render(ICommandList commands, World world, ITexture colorTarget)
    {
        Extractor.Extract(world, _scene);
        Render(commands, _scene, colorTarget);
    }

    /// <summary>Draws the world through the given camera into the color target; leaves it in the RenderTarget state.</summary>
    public void Render(ICommandList commands, World world, ITexture colorTarget, in Camera camera, in Transform cameraTransform)
    {
        Extractor.Extract(world, _scene);
        RenderView(commands, colorTarget, camera, cameraTransform);
    }

    /// <summary>Draws an already filled scene from its first camera, or clears the target without one; leaves it in the RenderTarget state.</summary>
    public void Render(ICommandList commands, RenderScene scene, ITexture colorTarget)
    {
        if (!ReferenceEquals(scene, _scene))
        {
            CopyScene(scene);
        }

        if (_scene.Cameras.Count == 0)
        {
            commands.Barrier(colorTarget, ResourceState.RenderTarget);
            ColorTarget clear = new(colorTarget, LoadAction.Clear, Background);
            commands.BeginRendering([clear]);
            commands.EndRendering();
            return;
        }

        RenderCamera first = _scene.Cameras[0];
        RenderView(commands, colorTarget, first.Camera, first.Transform);
    }

    public void Dispose()
    {
        Pipeline.Dispose();
        _frameData.Dispose();
        _probes.Dispose();
        _targets.Dispose();
        _textures.Dispose();
        _clampSampler.Dispose();
        _sampler.Dispose();
        _white.Dispose();
    }

    private void RenderView(ICommandList commands, ITexture colorTarget, in Camera camera, in Transform cameraTransform)
    {
        int width = colorTarget.Description.Width;
        int height = colorTarget.Description.Height;
        _targets.Ensure(width, height);

        RenderContext context = _context;
        context.Reset();
        context.Commands = commands;
        context.ColorTarget = colorTarget;
        context.Width = width;
        context.Height = height;
        context.Camera = camera;
        context.View = CameraView.Create(camera, cameraTransform, width, height);
        context.Mood = Mood;
        context.LocalLights = LocalLights;
        context.Probes = Probes;
        context.FrameSlot = _frameData.Advance();

        Pipeline.Prepare(context);
        context.ProbeSource = FindProbes();
        IBuffer frame = _frameData.Write(context);
        context.FrameBuffer = frame.BindlessIndex;
        Pipeline.Execute(context);
    }

    private ProbeSource? FindProbes()
    {
        if (BakeProbes is not null)
        {
            return BakeProbes;
        }

        if (!Probes)
        {
            return null;
        }

        return _probes.Find(_scene);
    }

    private void CopyScene(RenderScene scene)
    {
        _scene.Clear();
        _scene.Cameras.AddRange(scene.Cameras);
        _scene.Meshes.AddRange(scene.Meshes);
        _scene.PointLights.AddRange(scene.PointLights);
        _scene.SpotLights.AddRange(scene.SpotLights);
        _scene.ProbeVolumes.AddRange(scene.ProbeVolumes);
    }
}
