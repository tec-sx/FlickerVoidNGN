using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.RayTracing;
using FlickerVoid.Engine.Rendering.Shadows;
using FlickerVoid.Engine.Rendering.Sky;
using FlickerVoid.Engine.Rendering.Upscaling;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Draws a frame: extracts the world into a <see cref="RenderScene"/>, lets every pass of the <see cref="Pipeline"/> prepare,
/// uploads the frame's view, lights, shadows, clusters and probes, then records the passes in order. The default passes skin
/// meshes, gather lights, draw the shadow atlas, build the scene's acceleration structures and trace its irradiance probes when it
/// traces rays, draw the depth and G-buffer and the screen shadows, light the G-buffer into HDR, fill the sky and lay the depth
/// fade on it, post-process it graded as the <see cref="Mood"/> says, show a <see cref="DebugView"/> in its place when one is
/// asked for, and draw the outlines of outlined meshes.
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
    private readonly ProbePass _probePass;
    private readonly RenderScene _scene = new();
    private readonly RenderContext _context;
    private readonly JitterSequence _jitter = new();
    private IUpscaler? _upscaler;
    private ISampler? _sharpSampler;
    private uint _cuts;
    private bool _drawn;

    public SceneRenderer(IGraphicsDevice device, IContentManager content, PixelFormat colorFormat)
    {
        Device = device;
        Content = content;
        ColorFormat = colorFormat;
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
        _probePass = new ProbePass(device, shaders, _probes);
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
        Pipeline.Add(new RayTracingPass(device));
        Pipeline.Add(_probePass);
        Pipeline.Add(new GeometryPass(device, shaders, content));
        Pipeline.Add(new ShadowRayPass(device, shaders));
        Pipeline.Add(new ScreenShadowPass(device, shaders));
        Pipeline.Add(new LightingPass(device, shaders));
        Pipeline.Add(new SkyPass(device, shaders));
        Pipeline.Add(new DepthFadePass(device, shaders));
        Pipeline.Add(new UpscalePass(device, shaders));
        Pipeline.Add(new PostProcessPass(device, shaders, colorFormat, content));
        Pipeline.Add(new DebugViewPass(device, shaders, colorFormat));
        Pipeline.Add(new OutlinePass(device, shaders, colorFormat));
    }

    public IGraphicsDevice Device { get; }

    public IContentManager Content { get; }

    /// <summary>The format of the color targets it draws into.</summary>
    public PixelFormat ColorFormat { get; }

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

    /// <summary>
    /// Whether the scene's first irradiance volume's probes follow the light, traced by rays every frame, while it draws with the
    /// ray traced tier; off, or without ray tracing, it lights with the probes baked into the volume's file.
    /// </summary>
    public bool LiveProbes { get; set; } = true;

    /// <summary>
    /// A renderer whose probes it lights with in place of tracing its own, such as the one drawing the same scene's main view; it
    /// draws after that renderer each frame. Null traces or loads its own.
    /// </summary>
    public SceneRenderer? ProbesFrom { get; set; }

    /// <summary>
    /// Whether a sky gradient fills what the scene leaves empty; the game's window and the editor's views draw it, while stages,
    /// previews and probe bakes leave the background empty.
    /// </summary>
    public bool Sky { get; set; }

    /// <summary>What it shows: the lit frame by default, or one of the G-buffer's targets or the depth in its place.</summary>
    public DebugView DebugView { get; set; }

    /// <summary>
    /// Whether it draws with the ray traced tier where the device has ray queries, building the scene's acceleration structures
    /// each frame for its ray traced passes; the game's window, the editor's viewport and probe bakes do, while stages and previews
    /// draw without. Ignored on devices without ray queries.
    /// </summary>
    public bool RayTracing { get; set; }

    /// <summary>
    /// How frames are upscaled where the device has DLSS: off by default, drawing at the output's size; Quality draws at about
    /// two thirds of it and DLAA at its size, jittered, and DLSS fills the output in. Ignored on devices without DLSS.
    /// </summary>
    public UpscaleMode Upscaling { get; set; }

    /// <summary>
    /// Whether DLSS Ray Reconstruction denoises the ray traced shadows and occlusion as it upscales, in place of the renderer's
    /// own filter of them, where frames trace rays and are upscaled on a device that has it; on by default.
    /// </summary>
    public bool RayReconstruction { get; set; } = true;

    /// <summary>Whether the depth fade is drawn; on by default.</summary>
    public bool DepthFades { get; set; } = true;

    /// <summary>The depth fade drawn in place of the mood's, such as a camera preview's in an editor; null draws the mood's.</summary>
    public DepthFade? DepthFade { get; set; }

    /// <summary>The world point the depth fade is measured from in place of the scene's; null keeps the scene's.</summary>
    public Vector3? Focus { get; set; }

    /// <summary>
    /// Turns mesh names into models; the game's (a service from <c>AddRendering</c>, which the game's renderers are set up with) also makes
    /// the meshes its modules add sources for, such as creations' parts.
    /// </summary>
    public MeshSources Meshes { get; set; } = new();

    /// <summary>What the last frame drew and culled.</summary>
    public RenderStatistics Statistics => _context.Statistics;

    /// <summary>The pass that finds the frame's probes and traces them live, which the baker bakes through.</summary>
    internal ProbePass ProbePass => _probePass;

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
            _scene.CopyFrom(scene);
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

    /// <summary>Draws an already filled scene through the given camera; leaves the target in the RenderTarget state.</summary>
    public void Render(ICommandList commands, RenderScene scene, ITexture colorTarget, in Camera camera, in Transform cameraTransform)
    {
        if (!ReferenceEquals(scene, _scene))
        {
            _scene.CopyFrom(scene);
        }

        RenderView(commands, colorTarget, camera, cameraTransform);
    }

    public void Dispose()
    {
        _upscaler?.Dispose();
        _sharpSampler?.Dispose();
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
        bool rayTracing = RayTracing && Device.Features.HasFlag(GraphicsFeatures.RayQueries);
        bool newUpscaler = EnsureUpscaler(width, height, rayTracing);
        int renderWidth = width;
        int renderHeight = height;
        Vector2 jitter = Vector2.Zero;
        ISampler materialSampler = _sampler;
        bool denoised = false;
        if (_upscaler is { } upscaler)
        {
            renderWidth = upscaler.RenderWidth;
            renderHeight = upscaler.RenderHeight;
            denoised = upscaler.Description.Denoises;
            int phases = JitterSequence.Phases(renderWidth, width, denoised);
            jitter = _jitter.Next(phases);
            materialSampler = _sharpSampler!;
        }

        bool upscaled = _upscaler is not null;
        bool newTargets = _targets.Ensure(renderWidth, renderHeight, width, height, upscaled);
        bool cut = _scene.Cuts != _cuts;
        _cuts = _scene.Cuts;

        RenderContext context = _context;
        context.Reset();
        context.Commands = commands;
        context.ColorTarget = colorTarget;
        context.Width = width;
        context.Height = height;
        context.RenderWidth = renderWidth;
        context.RenderHeight = renderHeight;
        context.Jitter = jitter;
        context.Upscaler = _upscaler;
        context.MaterialSampler = materialSampler;
        context.ResetsHistory = !_drawn || newUpscaler || newTargets || cut;
        _drawn = true;
        RenderStatistics statistics = context.Statistics;
        statistics.RenderWidth = renderWidth;
        statistics.RenderHeight = renderHeight;
        statistics.OutputWidth = width;
        statistics.OutputHeight = height;
        statistics.Upscaled = upscaled;
        statistics.Denoised = denoised;
        context.Camera = camera;
        context.View = CameraView.Create(camera, cameraTransform, width, height);
        context.Mood = Mood;
        context.LocalLights = LocalLights;
        context.Probes = Probes;
        context.LiveProbes = LiveProbes;
        context.ProbesFrom = ProbesFrom?._probePass;
        context.Sky = Sky;
        context.DebugView = DebugView;
        context.RayTracing = rayTracing;
        context.DepthFade = default;
        if (DepthFades)
        {
            context.DepthFade = DepthFade ?? Mood.ToDepthFade();
        }

        context.Focus = Focus ?? _scene.Focus;
        context.Meshes = Meshes;
        context.FrameSlot = _frameData.Advance();

        Pipeline.Prepare(context);
        IBuffer frame = _frameData.Write(context);
        context.FrameBuffer = frame.BindlessIndex;
        Pipeline.Execute(context);
    }

    /// <summary>
    /// Makes an upscaler, with a sampler sharpening textures by its mip bias, when the mode, the output's size or whether it
    /// denoises the frame's traced rays changed, or drops them while upscaling is off or the device has no DLSS; says whether it
    /// made one.
    /// </summary>
    private bool EnsureUpscaler(int width, int height, bool rayTracing)
    {
        UpscaleMode mode = Upscaling;
        if (!Device.Features.HasFlag(GraphicsFeatures.Dlss))
        {
            mode = UpscaleMode.Off;
        }

        bool denoises = mode != UpscaleMode.Off && rayTracing && RayReconstruction && Device.Features.HasFlag(GraphicsFeatures.RayReconstruction);
        UpscalerDescription description = new(mode, width, height) { Denoises = denoises };
        if (_upscaler is not null && _upscaler.Description == description)
        {
            return false;
        }

        _upscaler?.Dispose();
        _upscaler = null;
        _sharpSampler?.Dispose();
        _sharpSampler = null;
        if (mode == UpscaleMode.Off)
        {
            return false;
        }

        _upscaler = Device.CreateUpscaler(description);
        float renderShare = (float)_upscaler.RenderWidth / width;
        float bias = MathF.Log2(renderShare) - 1;
        SamplerDescription sharp = new(SamplerFilter.Linear, SamplerAddressMode.Wrap) { MipBias = bias };
        _sharpSampler = Device.CreateSampler(sharp);
        return true;
    }
}
