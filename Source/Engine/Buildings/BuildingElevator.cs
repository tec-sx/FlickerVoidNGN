using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Buildings;

/// <summary>A building's elevator: the platform, resting on the lowest storey's floor, which stops at every storey's floor.</summary>
public sealed class BuildingElevator
{
    public required SolidPart Platform { get; set; }

    /// <summary>How fast the platform travels, in metres a second.</summary>
    public float Speed { get; set; } = 1.5f;
}
