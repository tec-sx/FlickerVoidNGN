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

    /// <summary>Indoor volumes' boxes, which scale the ambient light inside them.</summary>
    public List<RenderIndoor> Indoors { get; } = [];

    /// <summary>The indoor volume the camera looks into, cutting away what stands in front of it, if any.</summary>
    public RenderIndoor? Cutaway { get; set; }

    /// <summary>What the first camera's director lays over the frame, if anything.</summary>
    public RenderTransition Transition { get; set; }

    public void Clear()
    {
        Cameras.Clear();
        Meshes.Clear();
        PointLights.Clear();
        SpotLights.Clear();
        ProbeVolumes.Clear();
        Indoors.Clear();
        Cutaway = null;
        Transition = default;
    }
}
