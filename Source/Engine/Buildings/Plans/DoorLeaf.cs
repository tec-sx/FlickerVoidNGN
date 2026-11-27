using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>A door's leaf in the space of its hinge's pivot, the pivot and the hinge's axis in the building's space, and the leaf's middle from the pivot.</summary>
public sealed record DoorLeaf(SolidMesh Mesh, Vector3 Pivot, Vector3 Axis, Vector3 Middle);
