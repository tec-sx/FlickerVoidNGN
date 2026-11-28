using System.Numerics;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Buildings;

/// <summary>A box in a building: where its part stands in the building's space, and the box's size and middle in the part's space.</summary>
public readonly record struct BuildingRegion(Transform Place, Vector3 Size, Vector3 Middle);
