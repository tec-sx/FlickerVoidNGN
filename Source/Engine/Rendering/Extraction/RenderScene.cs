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

    public void Clear()
    {
        Cameras.Clear();
        Meshes.Clear();
        PointLights.Clear();
        SpotLights.Clear();
        ProbeVolumes.Clear();
    }
}
