using System.Numerics;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// A camera shot placed in a building: its name, where it stands and how it is turned (in the building's space), its orthographic
/// size, how much it follows the target across its view and drifts toward the pointer, and the triggers that switch to it.
/// </summary>
public sealed class BuildingShot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The level of the storey it was placed on.</summary>
    public int Level { get; set; }

    public Vector3 Position { get; set; }

    public Quaternion Rotation { get; set; } = Quaternion.Identity;

    /// <summary>How high the view is, in metres.</summary>
    public float Size { get; set; } = 7.5f;

    /// <summary>How much of the target's offset from the middle of the view the camera follows, from 0 to 1.</summary>
    public float Follow { get; set; } = 0.15f;

    /// <summary>How far, in metres, the camera drifts toward the pointer.</summary>
    public float Look { get; set; } = 0.1f;

    public List<ShotTrigger> Triggers { get; set; } = [];
}
