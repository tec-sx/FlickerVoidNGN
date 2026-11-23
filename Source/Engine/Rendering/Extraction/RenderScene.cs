using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>What the renderer draws in one frame, copied out of the game world so passes never read it directly.</summary>
public sealed class RenderScene
{
    public List<RenderCamera> Cameras { get; } = [];

    /// <summary>Static meshes first, then animated ones, each in query order.</summary>
    public List<RenderMesh> Meshes { get; } = [];

    public List<RenderPointLight> PointLights { get; } = [];

    public List<RenderSpotLight> SpotLights { get; } = [];

    public List<RenderProbeVolume> ProbeVolumes { get; } = [];

    /// <summary>What the first camera's director lays over the frame, if anything.</summary>
    public RenderTransition Transition { get; set; }

    /// <summary>The depth fade the first camera draws with.</summary>
    public DepthFade DepthFade { get; set; }

    /// <summary>The world point the first camera's depth fade is measured from; null measures from the camera.</summary>
    public Vector3? Focus { get; set; }

    public void Clear()
    {
        Cameras.Clear();
        Meshes.Clear();
        PointLights.Clear();
        SpotLights.Clear();
        ProbeVolumes.Clear();
        Transition = default;
        DepthFade = default;
        Focus = null;
    }
}
