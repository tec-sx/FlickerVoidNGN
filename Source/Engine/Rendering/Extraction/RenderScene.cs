using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;

using FlickerVoid.Engine.Rendering.Sky;

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

    /// <summary>The world point the first camera's depth fade is measured from; null measures from the camera.</summary>
    public Vector3? Focus { get; set; }

    /// <summary>How many times the first camera's director has cut to a new view (<see cref="ActiveCamera.Cuts"/>), so effects that build on earlier frames start afresh when it changes.</summary>
    public uint Cuts { get; set; }

    /// <summary>The painted sky the scene names, or null for the plain gradient.</summary>
    public RenderSky? Sky { get; set; }

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
        Focus = null;
        Cuts = 0;
        Sky = null;
    }

    /// <summary>Replaces what this scene holds with everything <paramref name="other"/> holds, such as a dissolve drawing another renderer's scene.</summary>
    public void CopyFrom(RenderScene other)
    {
        Clear();
        Cameras.AddRange(other.Cameras);
        Meshes.AddRange(other.Meshes);
        PointLights.AddRange(other.PointLights);
        SpotLights.AddRange(other.SpotLights);
        ProbeVolumes.AddRange(other.ProbeVolumes);
        Indoors.AddRange(other.Indoors);
        Cutaway = other.Cutaway;
        Transition = other.Transition;
        Focus = other.Focus;
        Cuts = other.Cuts;
        Sky = other.Sky;
    }
}
