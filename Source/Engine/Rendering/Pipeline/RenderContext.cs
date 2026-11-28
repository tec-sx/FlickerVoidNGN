using System.Numerics;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Shadows;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>What the passes of one frame share: the scene, the view, the targets and the data earlier passes worked out.</summary>
public sealed class RenderContext
{
    public RenderContext(RenderScene scene)
    {
        Scene = scene;
    }

    public RenderScene Scene { get; }

    public ICommandList Commands { get; internal set; } = null!;

    public ITexture ColorTarget { get; internal set; } = null!;

    /// <summary>The output's width: the color target's.</summary>
    public int Width { get; internal set; }

    /// <summary>The output's height: the color target's.</summary>
    public int Height { get; internal set; }

    /// <summary>The width the scene is drawn at before post-processing: the output's, or an upscaler's smaller one.</summary>
    public int RenderWidth { get; internal set; }

    /// <summary>The height the scene is drawn at before post-processing: the output's, or an upscaler's smaller one.</summary>
    public int RenderHeight { get; internal set; }

    /// <summary>
    /// How far the frame's view is moved for the upscaler, in render pixels right and down, from -0.5 to 0.5; zero without one.
    /// The frame data's view carries it, while <see cref="View"/> stays without it for passes drawing at the output's size.
    /// </summary>
    public Vector2 Jitter { get; internal set; }

    /// <summary>
    /// Whether what passes keep from frame to frame starts afresh this frame, as the view jumped or the targets were made anew:
    /// the first frame, a camera cut, and a change of size or upscaling.
    /// </summary>
    public bool ResetsHistory { get; internal set; }

    public Camera Camera { get; internal set; }

    public CameraView View { get; internal set; }

    public LightingMood Mood { get; internal set; } = new();

    /// <summary>Which of the frames in flight this is; per-frame buffers indexed by it are safe to overwrite.</summary>
    public int FrameSlot { get; internal set; }

    /// <summary>The bindless index of this frame's data buffer, set between Prepare and Execute.</summary>
    public uint FrameBuffer { get; internal set; }

    public bool LocalLights { get; internal set; } = true;

    public bool Probes { get; internal set; } = true;

    /// <summary>Whether the probes follow the light live while the frame traces rays (<see cref="SceneRenderer.LiveProbes"/>).</summary>
    public bool LiveProbes { get; internal set; } = true;

    /// <summary>The probe pass of the renderer whose probes this frame lights with in place of its own (<see cref="SceneRenderer.ProbesFrom"/>), or null.</summary>
    internal ProbePass? ProbesFrom { get; set; }

    /// <summary>Whether the sky fills what the scene left empty.</summary>
    public bool Sky { get; internal set; }

    /// <summary>What the frame shows: the lit frame, or one of the G-buffer's targets or the depth in its place.</summary>
    public DebugView DebugView { get; internal set; }

    /// <summary>Whether the lighting pass draws the flat look in place of the lit scene (<see cref="SceneRenderer.FlatLook"/>).</summary>
    public bool FlatLook { get; internal set; }

    /// <summary>Whether the frame traces rays: the renderer's <see cref="SceneRenderer.RayTracing"/> is on and the device has ray queries.</summary>
    public bool RayTracing { get; internal set; }

    /// <summary>
    /// The scene's top-level acceleration structure while the frame traces rays, set as the passes prepare and built before the
    /// geometry pass; each instance's id is its draw's number. Shaders find it in the frame data too. Null without ray tracing.
    /// </summary>
    public IAccelerationStructure? AccelerationStructure { get; internal set; }

    /// <summary>
    /// The lit fog's volume, integrated front to back, while the mood has fog (FogVolumePass), as a buffer the lighting pass reads
    /// through the frame data; null without fog.
    /// </summary>
    public IBuffer? FogVolume { get; internal set; }

    /// <summary>The depth fade laid on the lit scene (DepthFadePass).</summary>
    public DepthFade DepthFade { get; internal set; }

    /// <summary>The world point the depth fade is measured from; null measures from the camera.</summary>
    public Vector3? Focus { get; internal set; }

    /// <summary>The seconds the renderer has drawn for, each frame's step at most a tenth of a second: what weather moves by.</summary>
    public float Time { get; internal set; }

    /// <summary>Turns the scene's mesh names into models.</summary>
    public MeshSources Meshes { get; internal set; } = null!;

    /// <summary>What the frame drew and culled so far.</summary>
    public RenderStatistics Statistics { get; } = new();

    /// <summary>
    /// Where the passes' CPU and GPU times go while the renderer profiles (<see cref="SceneRenderer.Profiling"/>); null leaves
    /// the passes untimed.
    /// </summary>
    public RenderProfile? Profile { get; internal set; }

    /// <summary>
    /// The scene's depth at the render size, which passes after the geometry pass can test against, such as overlays drawn over
    /// the output, reading it at their pixel scaled from the output's size to the render size.
    /// </summary>
    public ITexture Depth => Targets.Depth;

    /// <summary>What the geometry pass left at every pixel for lighting, beside the depth.</summary>
    public GBuffer GBuffer => Targets.GBuffer;

    internal ScreenTargets Targets { get; set; } = null!;

    internal TextureLibrary Textures { get; set; } = null!;

    internal ISampler Sampler { get; set; } = null!;

    internal ISampler ClampSampler { get; set; } = null!;

    /// <summary>The sampler of the draws' textures: the wrapping one, sharpened by a mip bias while an upscaler fills the frame in.</summary>
    internal ISampler MaterialSampler { get; set; } = null!;

    /// <summary>The upscaler filling the output from the frame, or null when it is drawn at the output's size.</summary>
    internal IUpscaler? Upscaler { get; set; }

    /// <summary>
    /// Whether the upscaler denoises the frame's ray traced light (DLSS Ray Reconstruction), so the traced shadows and occlusion
    /// go to it unfiltered, drawn as white noise, with guides of the surfaces.
    /// </summary>
    internal bool Denoises => Upscaler is { Description.Denoises: true };

    internal ITexture White { get; set; } = null!;

    internal List<MeshDraw> Draws { get; } = [];

    internal List<GpuLight> Lights { get; } = [];

    internal List<LightBounds> LightBounds { get; } = [];

    internal List<GpuShadow> Shadows { get; } = [];

    internal List<ShadowView> ShadowViews { get; } = [];

    internal uint SunShadow { get; set; } = GpuLight.None;

    internal uint ShadowAtlas { get; set; } = GpuLight.None;

    /// <summary>The lights whose shadows the frame traces, by their place in <see cref="Lights"/>, after the sun's.</summary>
    internal List<uint> RayShadowLights { get; } = [];

    /// <summary>The traced shadows the lighting pass reads, the sun's in red and the lamps' after it, or null without ray tracing.</summary>
    internal ITexture? RayShadows { get; set; }

    /// <summary>
    /// The traced ambient occlusion the lighting pass reads in red, in place of the screen shadows' (the share of the ambient
    /// light that reaches the pixel, before the mood's strength), or null without ray tracing.
    /// </summary>
    internal ITexture? RayOcclusion { get; set; }

    internal ProbeSource? ProbeSource { get; set; }

    /// <summary>The mood's canvas the frame is laid on, found as the passes prepare.</summary>
    internal PostCanvas Canvas { get; set; }

    /// <summary>The mood's lens dirt, found as the passes prepare, or null for a clean lens.</summary>
    internal ITexture? Dirt { get; set; }

    /// <summary>The lens's flare glass, found as the passes prepare, or null for no flare.</summary>
    internal FlareFile? Flare { get; set; }

    /// <summary>Empties what the last frame's passes worked out.</summary>
    internal void Reset()
    {
        Statistics.Reset();
        Draws.Clear();
        Lights.Clear();
        LightBounds.Clear();
        Shadows.Clear();
        ShadowViews.Clear();
        SunShadow = GpuLight.None;
        ShadowAtlas = GpuLight.None;
        RayShadowLights.Clear();
        RayShadows = null;
        RayOcclusion = null;
        ProbeSource = null;
        AccelerationStructure = null;
        FogVolume = null;
    }
}
