using System.Numerics;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>A point of a shot's rail and a keyframe of its timeline, in the shot entity's space; the camera looks down its -Z.</summary>
public sealed class ShotKey
{
    public Vector3 Position { get; set; }

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    /// <summary>The orthographic height or field of view, by the camera's projection; 0 keeps the camera's own.</summary>
    public float Zoom { get; set; }

    /// <summary>Seconds from the start of the timeline.</summary>
    public float Time { get; set; }

    /// <summary>How the move into this key starts and ends.</summary>
    public ShotEase Ease { get; set; }

    /// <summary>Sent as a <c>ShotMarkerEvent</c> when the camera reaches the key, unless none.</summary>
    public Tag Marker { get; set; }
}
