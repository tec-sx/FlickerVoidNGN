using System.Numerics;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// A door's leaf in the space of its hinge's pivot, the pivot and the hinge's axis in the building's space, and from the pivot the
/// leaf's middle, its handle and the tip of its free edge at the foot; with the pane of glass in its window, when it has one.
/// </summary>
public sealed record DoorLeaf(SolidMesh Mesh, Vector3 Pivot, Vector3 Axis, Vector3 Middle, Vector3 Handle, Vector3 Free, SolidMesh? Glass = null);
