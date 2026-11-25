using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>One part of a vehicle: where its pivot stands in the vehicle's space, and its solid mesh in the pivot's space.</summary>
public sealed class VehiclePart
{
    public Transform Place { get; set; } = new();

    public required SolidMesh Mesh { get; set; }
}
