using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>
/// A directed camera: its entity's <c>camera</c> gives the lens and its keys a rail and a timeline in the entity's space. A shot
/// without keys is still at its entity's transform.
/// </summary>
public struct Shot
{
    public Shot()
    {
    }

    public List<ShotKey> Keys { get; set; } = [];

    public ShotDrive Drive { get; set; }

    /// <summary>Whether the scene starts on this shot; without one, the first shot found starts.</summary>
    public bool Start { get; set; }

    /// <summary>The name of the entity a tracked shot follows and the shot looks at; empty takes the first <see cref="CameraTarget"/>.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Whether the camera turns to look at the target instead of taking the keys' rotations.</summary>
    public bool LookAtTarget { get; set; }

    /// <summary>Seconds a tracked shot takes to come most of the way to its target's point on the rail; 0 follows at once.</summary>
    public float Smoothing { get; set; } = 0.3f;

    /// <summary>The camera rig (a <c>cameraRig</c> data asset) whose layers move the camera a little on top of the rail.</summary>
    [ContentFile(".fvdata")]
    public string Rig { get; set; } = string.Empty;

    /// <summary>Whether the player may skip it while it plays, as a cutscene, jumping to where it plays to.</summary>
    public bool Skippable { get; set; }
}
