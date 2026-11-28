using System.Numerics;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>What a ray met of a building's storey: the thing, how far along the ray, and where, in the building's space.</summary>
public readonly record struct PlanHit(PlanPick Pick, float Distance, Vector3 Point);
