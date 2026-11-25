using System.Numerics;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Samplers;
using FlickerVoid.Engine.Graphics.Textures;
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

    public int Width { get; internal set; }

    public int Height { get; internal set; }

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

    /// <summary>Whether the frame traces rays: the renderer's <see cref="SceneRenderer.RayTracing"/> is on and the device has ray queries.</summary>
    public bool RayTracing { get; internal set; }

    /// <summary>
    /// The scene's top-level acceleration structure while the frame traces rays, set as the passes prepare and built before the
    /// geometry pass; each instance's id is its draw's number. Shaders find it in the frame data too. Null without ray tracing.
    /// </summary>
    public IAccelerationStructure? AccelerationStructure { get; internal set; }

    /// <summary>The depth fade post-processing draws.</summary>
    public DepthFade DepthFade { get; internal set; }

    /// <summary>The world point the depth fade is measured from; null measures from the camera.</summary>
    public Vector3? Focus { get; internal set; }

    /// <summary>Turns the scene's mesh names into models.</summary>
    public MeshSources Meshes { get; internal set; } = null!;

    /// <summary>What the frame drew and culled so far.</summary>
    public RenderStatistics Statistics { get; } = new();

    /// <summary>The scene's depth, which passes after the geometry pass can test against, such as overlays drawn over the output.</summary>
    public ITexture Depth => Targets.Depth;

    /// <summary>What the geometry pass left at every pixel for lighting, beside the depth.</summary>
    public GBuffer GBuffer => Targets.GBuffer;

    internal ScreenTargets Targets { get; set; } = null!;

    internal TextureLibrary Textures { get; set; } = null!;

    internal ISampler Sampler { get; set; } = null!;

    internal ISampler ClampSampler { get; set; } = null!;

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
    }
}
